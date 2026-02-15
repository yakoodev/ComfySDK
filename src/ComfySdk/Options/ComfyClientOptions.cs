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
}
