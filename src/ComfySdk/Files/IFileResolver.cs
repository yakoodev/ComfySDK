namespace ComfySdk.Files;

/// <summary>Resolves file inputs into bytes/streams suitable for upload.</summary>
public interface IFileResolver
{
    /// <summary>Resolves file content and metadata from user-provided input.</summary>
    ValueTask<ResolvedFile> ResolveAsync(FileInput input, CancellationToken cancellationToken = default);
}

/// <summary>Resolved file content.</summary>
public sealed record ResolvedFile(string FileName, string? ContentType, byte[] Content);
