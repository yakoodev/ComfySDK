using ComfySdk.Abstractions;
using ComfySdk.Routing;

namespace ComfySdk.Options;

/// <summary>Configuration options for <c>ComfyClient</c>.</summary>
public sealed class ComfyClientOptions
{
    /// <summary>Target endpoint flavor used by SDK strategies.</summary>
    public ComfyEndpointKind EndpointKind { get; init; } = ComfyEndpointKind.Server;

    /// <summary>Base URL of Comfy endpoint (Server or Cloud).</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Optional API prefix, e.g. <c>/api</c> for Cloud.</summary>
    public string ApiPrefix { get; init; } = string.Empty;

    /// <summary>Route map for endpoint-specific route overrides.</summary>
    public RouteMap RouteMap { get; init; } = new();

    /// <summary>Optional authentication provider for outgoing requests.</summary>
    public IAuthProvider? AuthProvider { get; init; }

    /// <summary>Default request timeout.</summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Upload-specific timeout.</summary>
    public TimeSpan UploadTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Download-specific timeout.</summary>
    public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Retry settings for transient failures.</summary>
    public ComfyRetryOptions Retry { get; init; } = new();

    /// <summary>Enables automatic WS reconnect behavior.</summary>
    public bool EnableWsReconnect { get; init; } = true;

    /// <summary>Maximum reconnect attempts for WS stream.</summary>
    public int WsMaxReconnectAttempts { get; init; } = 3;

    /// <summary>Base delay between WS reconnect attempts.</summary>
    public TimeSpan WsReconnectBaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Builds absolute endpoint URI using <see cref="ApiPrefix"/> and <see cref="RouteMap"/> values.</summary>
    public Uri BuildEndpoint(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);

        var prefix = ApiPrefix.Trim('/');
        var routePart = route.Trim('/');
        var combined = string.IsNullOrWhiteSpace(prefix)
            ? routePart
            : $"{prefix}/{routePart}";
        return new Uri(BaseUrl, "/" + combined);
    }

    /// <summary>Builds absolute WS endpoint URI using <see cref="RouteMap.WsPath"/>.</summary>
    public Uri BuildWsEndpoint()
    {
        var wsRoute = BuildEndpoint(RouteMap.WsPath);
        var builder = new UriBuilder(wsRoute)
        {
            Scheme = wsRoute.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Port = wsRoute.Port,
        };
        return builder.Uri;
    }
}
