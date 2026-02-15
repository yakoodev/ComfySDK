using System.Net.Http.Headers;
using ComfySdk.Abstractions;

namespace ComfySdk.Files;

/// <summary>Uploads files via cloud upload endpoint.</summary>
public sealed class CloudFileUploader : IFileUploader
{
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly IAuthProvider? _authProvider;

    public CloudFileUploader(HttpClient httpClient, Uri baseUri, IAuthProvider? authProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        _authProvider = authProvider;
    }

    public async ValueTask<string> UploadAsync(ResolvedFile file, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var payload = new ByteArrayContent(file.Content);
        payload.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType ?? "application/octet-stream");
        content.Add(payload, "file", file.FileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "/api/files/upload"))
        {
            Content = content
        };
        if (_authProvider is not null)
        {
            await _authProvider.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return file.FileName;
    }
}
