namespace ComfySdk.Models;

/// <summary>Represents a single output from Comfy history/view endpoints.</summary>
public sealed record OutputArtifact(string Name, string Type, Uri? Url = null, string? SavedPath = null, byte[]? Data = null);
