namespace ComfySdk.Files;

/// <summary>Uploads prepared file content to Comfy endpoints.</summary>
public interface IFileUploader
{
    /// <summary>Uploads a file and returns server-specific reference string.</summary>
    ValueTask<string> UploadAsync(ResolvedFile file, CancellationToken cancellationToken = default);
}
