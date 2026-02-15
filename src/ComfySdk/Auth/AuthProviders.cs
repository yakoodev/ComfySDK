namespace ComfySdk.Auth;

/// <summary>Factory helpers for common authentication providers.</summary>
public static class AuthProviders
{
    /// <summary>No authentication.</summary>
    public static Abstractions.IAuthProvider None() => NoneAuthProvider.Instance;

    /// <summary>Bearer token authentication using <c>Authorization: Bearer ...</c>.</summary>
    public static Abstractions.IAuthProvider Bearer(string token) => new BearerAuthProvider(token);

    /// <summary>API key in header authentication.</summary>
    public static Abstractions.IAuthProvider ApiKeyHeader(string headerName, string apiKey) => new ApiKeyHeaderAuthProvider(headerName, apiKey);

    /// <summary>Cookie authentication.</summary>
    public static Abstractions.IAuthProvider Cookie(string cookieName, string cookieValue) => new CookieAuthProvider(cookieName, cookieValue);
}
