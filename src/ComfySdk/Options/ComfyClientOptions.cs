using ComfySdk.Abstractions;
using ComfySdk.Routing;

namespace ComfySdk.Options;

/// <summary>Configuration options for <c>ComfyClient</c>.</summary>
public sealed class ComfyClientOptions
{
    /// <summary>Base URL of Comfy endpoint (Server or Cloud).</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Optional API prefix, e.g. <c>/api</c> for Cloud.</summary>
    public string ApiPrefix { get; init; } = string.Empty;

    /// <summary>Route map for endpoint-specific route overrides.</summary>
    public RouteMap RouteMap { get; init; } = new();

    /// <summary>Optional authentication provider for outgoing requests.</summary>
    public IAuthProvider? AuthProvider { get; init; }

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
