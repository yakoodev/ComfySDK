namespace ComfySdk.Files;

/// <summary>HTTP downloader for output artifacts, with redirect handling.</summary>
public sealed class DefaultDownloader : IDownloader
{
    private readonly HttpClient _httpClient;

    public DefaultDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        });
    }

    public async ValueTask<byte[]> DownloadAsync(Uri url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }
}
