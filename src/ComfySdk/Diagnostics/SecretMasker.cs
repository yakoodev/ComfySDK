namespace ComfySdk.Diagnostics;

/// <summary>Helpers that remove secrets from request data before logging.</summary>
public static class SecretMasker
{
    /// <summary>Masks full request line including query and sensitive headers.</summary>
    public static string FormatRequestForLog(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var uri = request.RequestUri ?? new Uri("about:blank");
        var maskedUri = MaskUri(uri);
        var headers = request.Headers.Select(h => $"{h.Key}={MaskHeaderValue(h.Key, string.Join(",", h.Value))}");
        return $"{request.Method} {maskedUri} | {string.Join("; ", headers)}";
    }

    /// <summary>Masks known-sensitive query parameters.</summary>
    public static string MaskUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (string.IsNullOrEmpty(uri.Query))
        {
            return uri.ToString();
        }

        var basePart = uri.GetLeftPart(UriPartial.Path);
        var query = uri.Query.TrimStart('?');
        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(MaskQueryParameter);
        return $"{basePart}?{string.Join("&", parts)}";
    }

    /// <summary>Masks header values if header name is secret-like.</summary>
    public static string MaskHeaderValue(string headerName, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        value ??= string.Empty;

        return IsSensitiveToken(headerName)
            ? "***"
            : value;
    }

    private static string MaskQueryParameter(string part)
    {
        var pair = part.Split('=', 2);
        var key = Uri.UnescapeDataString(pair[0]);
        if (pair.Length == 1)
        {
            return part;
        }

        return IsSensitiveToken(key)
            ? $"{pair[0]}=***"
            : part;
    }

    private static bool IsSensitiveToken(string key)
    {
        var normalized = key.ToLowerInvariant();
        return normalized.Contains("authorization", StringComparison.Ordinal)
            || normalized.Contains("token", StringComparison.Ordinal)
            || normalized.Contains("api-key", StringComparison.Ordinal)
            || normalized.Contains("apikey", StringComparison.Ordinal)
            || normalized.Contains("key", StringComparison.Ordinal)
            || normalized.Contains("secret", StringComparison.Ordinal)
            || normalized.Contains("cookie", StringComparison.Ordinal)
            || normalized.Contains("signature", StringComparison.Ordinal);
    }
}
