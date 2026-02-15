namespace ComfySdk.Models;

/// <summary>Represents a single normalized event for workflow execution lifecycle.</summary>
public sealed record RunEvent(RunEventType Type, string? Message = null, int? ProgressPercent = null, string? NodeId = null);
