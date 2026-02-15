using ComfySdk.Files;
using ComfySdk.Models;
using ComfySdk.Outputs;

namespace ComfySdk.Tests;

public static class OutputSelectionAndDownloadTests
{
    public static void GlobNameFilter_SelectsExpectedOutputs()
    {
        var artifacts = new[]
        {
            new OutputArtifact("cat.png", "image"),
            new OutputArtifact("dog.png", "image"),
            new OutputArtifact("clip.mp4", "video")
        };

        var settings = new OutputSelectionSettings
        {
            Mode = OutputSelectionMode.ByName,
            Types = ["image"],
            NamePatterns = ["c*.png"]
        };

        var selected = OutputSelector.Select(artifacts, settings);
        Ensure(selected.Count == 1, $"Expected 1 artifact, got {selected.Count}.");
        Ensure(selected[0].Name == "cat.png", $"Expected cat.png, got {selected[0].Name}.");
    }

    public static async Task FirstMode_DownloadsOnlyFirstAsBytes()
    {
        var artifacts = new[]
        {
            new OutputArtifact("a.png", "image", new Uri("https://example.test/a.png")),
            new OutputArtifact("b.png", "image", new Uri("https://example.test/b.png"))
        };
        var downloader = new FakeDownloader([1, 2, 3]);
        var service = new OutputDownloadService(downloader);
        var settings = new OutputSelectionSettings
        {
            Mode = OutputSelectionMode.First,
            Types = ["image"],
            Download = OutputDownloadMode.Bytes
        };

        var selected = await service.SelectAndDownloadAsync(artifacts, settings);
        Ensure(selected.Count == 1, $"Expected one selected artifact, got {selected.Count}.");
        var data = selected[0].Data;
        Ensure(data is not null && data.Length == 3, "Expected bytes payload.");
        Ensure(downloader.CallCount == 1, $"Expected one download call, got {downloader.CallCount}.");
    }

    public static async Task FilesMode_UsesGuidNameByDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"comfysdk-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var artifact = new OutputArtifact("result.mp4", "video", new Uri("https://example.test/result.mp4"));
            var downloader = new FakeDownloader([7, 8, 9]);
            var service = new OutputDownloadService(downloader);
            var settings = new OutputSelectionSettings
            {
                Mode = OutputSelectionMode.All,
                Types = ["video"],
                Download = OutputDownloadMode.Files,
                SaveDir = tempDir,
                FileNameMode = OutputFileNameMode.Guid
            };

            var selected = await service.SelectAndDownloadAsync([artifact], settings);
            var savedPath = selected[0].SavedPath;
            Ensure(!string.IsNullOrWhiteSpace(savedPath), "Expected saved file path.");
            Ensure(File.Exists(savedPath), "Expected file to be written.");
            Ensure(Path.GetFileName(savedPath) != "result.mp4", "Expected GUID-based file name.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakeDownloader(byte[] payload) : IDownloader
    {
        public int CallCount { get; private set; }

        public ValueTask<byte[]> DownloadAsync(Uri url, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(payload.ToArray());
        }
    }
}
