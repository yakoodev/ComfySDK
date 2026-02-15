using ComfySdk.Abstractions;

namespace ComfySdk.Auth;

/// <summary>Bearer token auth provider.</summary>
public sealed class BearerAuthProvider : IAuthProvider
{
    private readonly string _token;

    /// <summary>Creates bearer provider.</summary>
    public BearerAuthProvider(string token)
    {
        _token = string.IsNullOrWhiteSpace(token)
            ? throw new ArgumentException("Token must not be empty.", nameof(token))
            : token;
    }

    /// <inheritdoc />
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = cancellationToken;
        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_token}");
        return ValueTask.CompletedTask;
    }
}
