using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ComfySdk.Files;

/// <summary>Resolves file inputs and uploads with optional content-hash deduplication.</summary>
public sealed class FileUploadService
{
    private readonly IFileResolver _resolver;
    private readonly IFileUploader _uploader;
    private readonly bool _enableCache;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    public FileUploadService(IFileResolver resolver, IFileUploader uploader, bool enableCache = true)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _uploader = uploader ?? throw new ArgumentNullException(nameof(uploader));
        _enableCache = enableCache;
    }

    public async ValueTask<string> ResolveAndUploadAsync(FileInput input, CancellationToken cancellationToken = default)
    {
        var file = await _resolver.ResolveAsync(input, cancellationToken).ConfigureAwait(false);
        var hash = ComputeHash(file.Content);

        if (_enableCache && _cache.TryGetValue(hash, out var existing))
        {
            return existing;
        }

        var reference = await _uploader.UploadAsync(file, cancellationToken).ConfigureAwait(false);
        if (_enableCache)
        {
            _cache[hash] = reference;
        }

        return reference;
    }

    private static string ComputeHash(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
