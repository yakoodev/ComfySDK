namespace ComfySdk.Models;

/// <summary>Parameters for downloading view/output artifacts.</summary>
public sealed record ViewParams(string PathOrUrl, IReadOnlyDictionary<string, string>? Query = null);
