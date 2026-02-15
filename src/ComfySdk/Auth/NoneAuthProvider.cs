using ComfySdk.Abstractions;

namespace ComfySdk.Auth;

/// <summary>Auth provider that does not modify request.</summary>
public sealed class NoneAuthProvider : IAuthProvider
{
    /// <summary>Singleton instance.</summary>
    public static NoneAuthProvider Instance { get; } = new();

    private NoneAuthProvider()
    {
    }

    /// <inheritdoc />
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }
}
