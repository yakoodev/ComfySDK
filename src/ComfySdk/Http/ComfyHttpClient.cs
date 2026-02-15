using System.Net;
using System.Net.Http.Headers;
using ComfySdk.Exceptions;
using ComfySdk.Options;
using Microsoft.Extensions.Logging;

namespace ComfySdk.Http;

/// <summary>HTTP transport with retry, timeout and unified exception behavior.</summary>
public sealed class ComfyHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ComfyClientOptions _options;
    private readonly ILogger<ComfyHttpClient> _logger;
    private readonly Random _random = new();

    /// <summary>Creates HTTP transport.</summary>
    public ComfyHttpClient(HttpClient httpClient, ComfyClientOptions options, ILogger<ComfyHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Sends request with retry for transient failures.</summary>
    public async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        string route,
        string? promptId,
        ComfyRequestKind requestKind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);

        var maxRetries = Math.Max(0, _options.Retry.MaxRetries);
        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            using var timeoutCts = new CancellationTokenSource(GetTimeout(requestKind));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                var response = await _httpClient.SendAsync(request, linkedCts.Token);
                var requestId = TryGetHeader(response.Headers, "x-request-id")
                    ?? TryGetHeader(response.Headers, "request-id");

                if (ShouldRetry(response.StatusCode) && attempt < maxRetries)
                {
                    var delay = GetDelay(attempt);
                    _logger.LogWarning(
                        "Transient HTTP status {Status} on route {Route}. attempt={Attempt} requestId={RequestId} promptId={PromptId}",
                        (int)response.StatusCode,
                        route,
                        attempt + 1,
                        requestId,
                        promptId);
                    response.Dispose();
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var snippet = await ReadBodySnippetAsync(response);
                    throw new ComfyException(
                        message: $"Comfy request failed with HTTP {(int)response.StatusCode}.",
                        route: route,
                        httpStatus: (int)response.StatusCode,
                        requestId: requestId,
                        promptId: promptId,
                        bodySnippet: snippet);
                }

                _logger.LogInformation(
                    "HTTP request succeeded status={Status} route={Route} requestId={RequestId} promptId={PromptId}",
                    (int)response.StatusCode,
                    route,
                    requestId,
                    promptId);

                return response;
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < maxRetries)
            {
                var delay = GetDelay(attempt);
                _logger.LogWarning(
                    ex,
                    "Transient exception on route {Route}. attempt={Attempt} promptId={PromptId}",
                    route,
                    attempt + 1,
                    promptId);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ComfyException(
                    message: "Comfy request timed out.",
                    route: route,
                    promptId: promptId,
                    innerException: ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ComfyException(
                    message: "Comfy request failed with network error.",
                    route: route,
                    promptId: promptId,
                    innerException: ex);
            }
        }
    }

    /// <summary>Sends GET request and follows redirect responses manually.</summary>
    public async Task<HttpResponseMessage> GetWithRedirectsAsync(
        Uri startUri,
        string route,
        string? promptId,
        int maxRedirects = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startUri);

        var current = startUri;
        for (var redirect = 0; redirect <= maxRedirects; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var timeoutCts = new CancellationTokenSource(GetTimeout(ComfyRequestKind.Download));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, linkedCts.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ComfyException("Download request timed out.", route, promptId: promptId, innerException: ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ComfyException("Download request failed with network error.", route, promptId: promptId, innerException: ex);
            }

            if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
            {
                var target = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                _logger.LogInformation(
                    "Following redirect status={Status} route={Route} from={From} to={To} promptId={PromptId}",
                    (int)response.StatusCode,
                    route,
                    current,
                    target,
                    promptId);
                response.Dispose();
                current = target;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var requestId = TryGetHeader(response.Headers, "x-request-id")
                    ?? TryGetHeader(response.Headers, "request-id");
                var snippet = await ReadBodySnippetAsync(response);
                response.Dispose();
                throw new ComfyException(
                    message: $"Comfy download failed with HTTP {(int)response.StatusCode}.",
                    route: route,
                    httpStatus: (int)response.StatusCode,
                    requestId: requestId,
                    promptId: promptId,
                    bodySnippet: snippet);
            }

            return response;
        }

        throw new ComfyException("Download redirect limit exceeded.", route, promptId: promptId);
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is 300 or 301 or 302 or 303 or 307 or 308;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 429 || code >= 500;
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is HttpRequestException;
    }

    private TimeSpan GetDelay(int attempt)
    {
        var baseMs = _options.Retry.BaseDelay.TotalMilliseconds;
        var exp = Math.Pow(2, attempt);
        var jitter = _random.NextDouble() * _options.Retry.MaxJitter.TotalMilliseconds;
        var totalMs = Math.Min(10_000, (baseMs * exp) + jitter);
        return TimeSpan.FromMilliseconds(totalMs);
    }

    private TimeSpan GetTimeout(ComfyRequestKind requestKind)
    {
        return requestKind switch
        {
            ComfyRequestKind.Upload => _options.UploadTimeout,
            ComfyRequestKind.Download => _options.DownloadTimeout,
            _ => _options.DefaultTimeout,
        };
    }

    private static async Task<string?> ReadBodySnippetAsync(HttpResponseMessage response)
    {
        if (response.Content is null)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync();
        return body.Length <= 512 ? body : body[..512];
    }

    private static string? TryGetHeader(HttpResponseHeaders headers, string name)
    {
        return headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    }
}
