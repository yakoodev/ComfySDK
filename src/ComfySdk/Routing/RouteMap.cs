namespace ComfySdk.Routing;

/// <summary>Endpoint map for Comfy Server/Cloud route differences.</summary>
public sealed record RouteMap(
    string SubmitPrompt = "/prompt",
    string HistoryV1 = "/history",
    string HistoryV2 = "/history_v2",
    string View = "/view",
    string UploadImage = "/upload/image",
    string UploadMask = "/upload/mask",
    string Interrupt = "/interrupt",
    string Queue = "/queue",
    string Status = "/queue",
    string WsPath = "/ws");
