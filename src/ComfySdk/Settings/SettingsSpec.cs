using System.Text.Json.Nodes;
using ComfySdk.Outputs;
using ComfySdk.Workflow;

namespace ComfySdk.Settings;

public static class SettingsSchema
{
    public const int Version1 = 1;
}

/// <summary>Settings contract for generated parameters and materialization.</summary>
public sealed record SettingsSpec
{
    public int SettingsSchemaVersion { get; init; } = SettingsSchema.Version1;

    public IReadOnlyList<SettingsParameterSpec> Parameters { get; init; } = [];

    public SettingsOutputsSpec? Outputs { get; init; }

    public SettingsDiagnosticsSpec? Diagnostics { get; init; }
}

/// <summary>Single parameter definition with selector/path patch target.</summary>
public sealed record SettingsParameterSpec
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required NodeSelector Selector { get; init; }

    public required string Path { get; init; }

    public bool Required { get; init; }

    public JsonNode? Default { get; init; }

    public string? Description { get; init; }

    public SettingsFileParameterSpec? File { get; init; }
}

/// <summary>File conversion policy for file parameters.</summary>
public sealed record SettingsFileParameterSpec
{
    public string Conversion { get; init; } = "upload";
}

/// <summary>Outputs selection block from settings spec.</summary>
public sealed record SettingsOutputsSpec
{
    public string Mode { get; init; } = "all";

    public IReadOnlyList<string> Types { get; init; } = ["any"];

    public IReadOnlyList<string> NamePatterns { get; init; } = [];

    public string Download { get; init; } = "none";

    public string? SaveDir { get; init; }

    public string FileName { get; init; } = "guid";

    public OutputSelectionSettings ToRuntimeSettings()
    {
        return new OutputSelectionSettings
        {
            Mode = ParseSelectionMode(Mode),
            Types = Types,
            NamePatterns = NamePatterns,
            Download = ParseDownloadMode(Download),
            SaveDir = SaveDir,
            FileNameMode = ParseFileNameMode(FileName)
        };
    }

    private static OutputSelectionMode ParseSelectionMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "all" => OutputSelectionMode.All,
            "first" => OutputSelectionMode.First,
            "byname" => OutputSelectionMode.ByName,
            _ => throw new InvalidOperationException($"Unsupported outputs.mode '{value}'.")
        };
    }

    private static OutputDownloadMode ParseDownloadMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "none" => OutputDownloadMode.None,
            "bytes" => OutputDownloadMode.Bytes,
            "files" => OutputDownloadMode.Files,
            _ => throw new InvalidOperationException($"Unsupported outputs.download '{value}'.")
        };
    }

    private static OutputFileNameMode ParseFileNameMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "guid" => OutputFileNameMode.Guid,
            "original" => OutputFileNameMode.Original,
            _ => throw new InvalidOperationException($"Unsupported outputs.fileName '{value}'.")
        };
    }
}

/// <summary>Diagnostics settings for materialization.</summary>
public sealed record SettingsDiagnosticsSpec
{
    public bool SaveMaterializedWorkflow { get; init; }

    public string? Directory { get; init; }
}
