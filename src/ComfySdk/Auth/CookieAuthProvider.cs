using ComfySdk.Abstractions;

namespace ComfySdk.Auth;

/// <summary>Cookie auth provider.</summary>
public sealed class CookieAuthProvider : IAuthProvider
{
    private readonly string _cookieName;
    private readonly string _cookieValue;

    /// <summary>Creates cookie auth provider.</summary>
    public CookieAuthProvider(string cookieName, string cookieValue)
    {
        _cookieName = string.IsNullOrWhiteSpace(cookieName)
            ? throw new ArgumentException("Cookie name must not be empty.", nameof(cookieName))
            : cookieName;
        _cookieValue = string.IsNullOrWhiteSpace(cookieValue)
            ? throw new ArgumentException("Cookie value must not be empty.", nameof(cookieValue))
            : cookieValue;
    }

    /// <inheritdoc />
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = cancellationToken;

        var cookie = $"{_cookieName}={_cookieValue}";
        if (request.Headers.TryGetValues("Cookie", out var existing))
        {
            var merged = string.Join("; ", existing.Append(cookie));
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", merged);
        }
        else
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        return ValueTask.CompletedTask;
    }
}
