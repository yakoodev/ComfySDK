namespace ComfySdk.Models;

/// <summary>Final run result returned by <c>ComfyClient.RunAsync</c>.</summary>
public sealed record RunResult(string PromptId, IReadOnlyList<OutputArtifact> Outputs);
