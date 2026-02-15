using System.Net;
using ComfySdk.Diagnostics;
using ComfySdk.Exceptions;
using ComfySdk.Http;
using ComfySdk.Options;
using Microsoft.Extensions.Logging.Abstractions;

await RunMaskingTestAsync();
await RunRetry429TestAsync();
await RunNoRetry400TestAsync();

Console.WriteLine("ComfySdk.Tests: PASS");
return 0;

static Task RunMaskingTestAsync()
{
    var request = new HttpRequestMessage(
        HttpMethod.Get,
        "https://api.comfy.org/api/history?token=super-secret-token&prompt_id=42");
    request.Headers.TryAddWithoutValidation("Authorization", "Bearer super-secret-token");
    request.Headers.TryAddWithoutValidation("Cookie", "session=secret-session");

    var masked = SecretMasker.FormatRequestForLog(request);

    AssertDoesNotContain(masked, "super-secret-token", "masking query/header token");
    AssertDoesNotContain(masked, "secret-session", "masking cookie token");
    return Task.CompletedTask;
}

static async Task RunRetry429TestAsync()
{
    var handler = new SequenceHandler(
        new HttpResponseMessage(HttpStatusCode.TooManyRequests),
        new HttpResponseMessage(HttpStatusCode.TooManyRequests),
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok"),
            Headers = { { "x-request-id", "req-429-ok" } },
        });

    using var httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri("https://api.comfy.org"),
    };

    var options = new ComfyClientOptions
    {
        BaseUrl = new Uri("https://api.comfy.org"),
        Retry = new ComfyRetryOptions
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxJitter = TimeSpan.Zero,
        },
        DefaultTimeout = TimeSpan.FromSeconds(5),
    };

    var transport = new ComfyHttpClient(httpClient, options, NullLogger<ComfyHttpClient>.Instance);
    using var response = await transport.SendWithRetryAsync(
        () => new HttpRequestMessage(HttpMethod.Get, "https://api.comfy.org/prompt"),
        route: "/prompt",
        promptId: "prompt-429",
        requestKind: ComfyRequestKind.Default);

    Assert(handler.AttemptCount == 3, $"expected 3 attempts for 429, got {handler.AttemptCount}");
    Assert(response.StatusCode == HttpStatusCode.OK, "expected final status 200 after retries");
}

static async Task RunNoRetry400TestAsync()
{
    var bad = new HttpResponseMessage(HttpStatusCode.BadRequest)
    {
        Content = new StringContent("bad-request-body"),
    };
    bad.Headers.Add("x-request-id", "req-400");

    var handler = new SequenceHandler(bad);
    using var httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri("https://api.comfy.org"),
    };

    var options = new ComfyClientOptions
    {
        BaseUrl = new Uri("https://api.comfy.org"),
        Retry = new ComfyRetryOptions
        {
            MaxRetries = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxJitter = TimeSpan.Zero,
        },
        DefaultTimeout = TimeSpan.FromSeconds(5),
    };

    var transport = new ComfyHttpClient(httpClient, options, NullLogger<ComfyHttpClient>.Instance);

    try
    {
        using var _ = await transport.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "https://api.comfy.org/prompt"),
            route: "/prompt",
            promptId: "prompt-400",
            requestKind: ComfyRequestKind.Default);
        throw new InvalidOperationException("expected ComfyException for 400");
    }
    catch (ComfyException ex)
    {
        Assert(handler.AttemptCount == 1, $"expected no retries for 400, got {handler.AttemptCount}");
        Assert(ex.HttpStatus == 400, $"expected status 400, got {ex.HttpStatus}");
        Assert(ex.Route == "/prompt", $"expected route '/prompt', got '{ex.Route}'");
        Assert(ex.RequestId == "req-400", $"expected requestId 'req-400', got '{ex.RequestId}'");
        Assert(ex.PromptId == "prompt-400", $"expected promptId 'prompt-400', got '{ex.PromptId}'");
        Assert(ex.BodySnippet?.Contains("bad-request-body", StringComparison.Ordinal) == true, "expected body snippet to include response body");
    }
}

static void AssertDoesNotContain(string text, string forbidden, string message)
{
    if (text.Contains(forbidden, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"assert failed: {message}. actual='{text}'");
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"assert failed: {message}");
    }
}

file sealed class SequenceHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;

    public SequenceHandler(params HttpResponseMessage[] responses)
    {
        _responses = new Queue<HttpResponseMessage>(responses);
    }

    public int AttemptCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AttemptCount++;
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("no response configured for request");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}
