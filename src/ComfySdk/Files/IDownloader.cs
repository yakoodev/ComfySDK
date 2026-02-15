namespace ComfySdk.Files;

/// <summary>Downloads output artifacts produced by Comfy workflow runs.</summary>
public interface IDownloader
{
    /// <summary>Downloads bytes by absolute or relative URL.</summary>
    ValueTask<byte[]> DownloadAsync(Uri url, CancellationToken cancellationToken = default);
}
