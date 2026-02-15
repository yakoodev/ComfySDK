namespace ComfySdk.Files;

/// <summary>Default resolver for path/url/base64/bytes/stream file inputs.</summary>
public sealed class DefaultFileResolver : IFileResolver
{
    private readonly HttpClient _httpClient;

    public DefaultFileResolver(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async ValueTask<ResolvedFile> ResolveAsync(FileInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input switch
        {
            FileInput.PathFileInput path => await ResolvePathAsync(path, cancellationToken).ConfigureAwait(false),
            FileInput.UrlFileInput url => await ResolveUrlAsync(url, cancellationToken).ConfigureAwait(false),
            FileInput.Base64FileInput base64 => ResolveBase64(base64),
            FileInput.BytesFileInput bytes => ResolveBytes(bytes),
            FileInput.StreamFileInput stream => await ResolveStreamAsync(stream, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported FileInput type: {input.GetType().Name}.")
        };
    }

    private static async ValueTask<ResolvedFile> ResolvePathAsync(
        FileInput.PathFileInput input,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllBytesAsync(input.Path, cancellationToken).ConfigureAwait(false);
        var fileName = Path.GetFileName(input.Path);
        return new ResolvedFile(fileName, GuessContentType(fileName), content);
    }

    private async ValueTask<ResolvedFile> ResolveUrlAsync(
        FileInput.UrlFileInput input,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(input.Url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var fileName = GuessFileNameFromUrl(input.Url);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? GuessContentType(fileName);
        return new ResolvedFile(fileName, contentType, content);
    }

    private static ResolvedFile ResolveBase64(FileInput.Base64FileInput input)
    {
        var content = Convert.FromBase64String(input.Base64);
        const string fileName = "input.bin";
        return new ResolvedFile(fileName, "application/octet-stream", content);
    }

    private static ResolvedFile ResolveBytes(FileInput.BytesFileInput input)
    {
        var fileName = string.IsNullOrWhiteSpace(input.FileName) ? "input.bin" : input.FileName;
        return new ResolvedFile(fileName, GuessContentType(fileName), input.Bytes);
    }

    private static async ValueTask<ResolvedFile> ResolveStreamAsync(
        FileInput.StreamFileInput input,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await input.Stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        var fileName = string.IsNullOrWhiteSpace(input.FileName) ? "input.bin" : input.FileName;
        return new ResolvedFile(fileName, GuessContentType(fileName), ms.ToArray());
    }

    private static string GuessFileNameFromUrl(Uri url)
    {
        var name = Path.GetFileName(url.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return FormattableString.Invariant($"download-{Guid.NewGuid():N}.bin");
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".txt" => "text/plain",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}
