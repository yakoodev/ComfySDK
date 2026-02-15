namespace ComfySdk.Models;

/// <summary>Normalized run event type produced by WS/HTTP flow.</summary>
public enum RunEventType
{
    Connected,
    Reconnected,
    Disconnected,
    Queued,
    Executing,
    Progress,
    NodeUiUpdated,
    Log,
    Succeeded,
    Failed,
}
