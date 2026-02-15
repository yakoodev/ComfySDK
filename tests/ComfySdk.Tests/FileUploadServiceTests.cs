using System.Text;
using ComfySdk.Files;

namespace ComfySdk.Tests;

public static class FileUploadServiceTests
{
    public static async Task Base64Input_ResolvesToBytes_AndUploads()
    {
        var expected = Encoding.UTF8.GetBytes("hello");
        var resolver = new DefaultFileResolver();
        var uploader = new CapturingUploader();
        var service = new FileUploadService(resolver, uploader, enableCache: true);

        var input = FileInput.FromBase64(Convert.ToBase64String(expected));
        _ = await service.ResolveAndUploadAsync(input);

        Ensure(uploader.UploadCount == 1, $"Expected single upload, got {uploader.UploadCount}.");
        Ensure(expected.SequenceEqual(uploader.LastContent ?? []), "Expected uploaded bytes to match decoded base64.");
    }

    public static async Task SameContent_IsUploadedOnce_WhenCacheEnabled()
    {
        var resolver = new DefaultFileResolver();
        var uploader = new CapturingUploader();
        var service = new FileUploadService(resolver, uploader, enableCache: true);

        var input = FileInput.FromBytes([1, 2, 3, 4], "a.bin");
        var ref1 = await service.ResolveAndUploadAsync(input);
        var ref2 = await service.ResolveAndUploadAsync(input);

        Ensure(uploader.UploadCount == 1, $"Expected deduplicated upload count 1, got {uploader.UploadCount}.");
        Ensure(ref1 == ref2, "Expected cached reference for repeated content.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingUploader : IFileUploader
    {
        public int UploadCount { get; private set; }

        public byte[]? LastContent { get; private set; }

        public ValueTask<string> UploadAsync(ResolvedFile file, CancellationToken cancellationToken = default)
        {
            UploadCount++;
            LastContent = file.Content.ToArray();
            return ValueTask.FromResult($"uploaded://{file.FileName}");
        }
    }
}
