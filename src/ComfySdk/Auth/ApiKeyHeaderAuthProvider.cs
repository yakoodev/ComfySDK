using ComfySdk.Abstractions;

namespace ComfySdk.Auth;

/// <summary>API key in custom header auth provider.</summary>
public sealed class ApiKeyHeaderAuthProvider : IAuthProvider
{
    private readonly string _headerName;
    private readonly string _apiKey;

    /// <summary>Creates API-key header auth provider.</summary>
    public ApiKeyHeaderAuthProvider(string headerName, string apiKey)
    {
        _headerName = string.IsNullOrWhiteSpace(headerName)
            ? throw new ArgumentException("Header name must not be empty.", nameof(headerName))
            : headerName;
        _apiKey = string.IsNullOrWhiteSpace(apiKey)
            ? throw new ArgumentException("API key must not be empty.", nameof(apiKey))
            : apiKey;
    }

    /// <inheritdoc />
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = cancellationToken;
        request.Headers.Remove(_headerName);
        request.Headers.TryAddWithoutValidation(_headerName, _apiKey);
        return ValueTask.CompletedTask;
    }
}
