using ComfySdk.Files;
using ComfySdk.Models;

namespace ComfySdk.Outputs;

public sealed class OutputDownloadService
{
    private readonly IDownloader _downloader;

    public OutputDownloadService(IDownloader downloader)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
    }

    public async ValueTask<IReadOnlyList<OutputArtifact>> SelectAndDownloadAsync(
        IEnumerable<OutputArtifact> artifacts,
        OutputSelectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var selected = OutputSelector.Select(artifacts, settings);
        if (settings.Download == OutputDownloadMode.None)
        {
            return selected;
        }

        var result = new List<OutputArtifact>(selected.Count);
        foreach (var artifact in selected)
        {
            if (artifact.Url is null)
            {
                result.Add(artifact);
                continue;
            }

            var bytes = await _downloader.DownloadAsync(artifact.Url, cancellationToken).ConfigureAwait(false);
            if (settings.Download == OutputDownloadMode.Bytes)
            {
                result.Add(artifact with { Data = bytes });
                continue;
            }

            var saveDir = string.IsNullOrWhiteSpace(settings.SaveDir)
                ? Path.Combine(Path.GetTempPath(), "ComfySdk")
                : settings.SaveDir;
            Directory.CreateDirectory(saveDir);
            var fileName = BuildFileName(artifact.Name, settings.FileNameMode);
            var fullPath = Path.Combine(saveDir, fileName);
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);
            result.Add(artifact with { SavedPath = fullPath });
        }

        return result;
    }

    private static string BuildFileName(string originalName, OutputFileNameMode mode)
    {
        if (mode == OutputFileNameMode.Original && !string.IsNullOrWhiteSpace(originalName))
        {
            return originalName;
        }

        var ext = Path.GetExtension(originalName);
        return string.IsNullOrWhiteSpace(ext)
            ? $"{Guid.NewGuid():N}"
            : $"{Guid.NewGuid():N}{ext}";
    }
}
