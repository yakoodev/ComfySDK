namespace ComfySdk.Outputs;

public enum OutputSelectionMode
{
    All,
    First,
    ByName
}

public enum OutputDownloadMode
{
    None,
    Bytes,
    Files
}

public enum OutputFileNameMode
{
    Guid,
    Original
}

public sealed record OutputSelectionSettings
{
    public OutputSelectionMode Mode { get; init; } = OutputSelectionMode.All;

    public IReadOnlyList<string> Types { get; init; } = ["any"];

    public IReadOnlyList<string> NamePatterns { get; init; } = [];

    public OutputDownloadMode Download { get; init; } = OutputDownloadMode.None;

    public string? SaveDir { get; init; }

    public OutputFileNameMode FileNameMode { get; init; } = OutputFileNameMode.Guid;
}
