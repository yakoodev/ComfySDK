using System.Net;
using ComfySdk;
using ComfySdk.Diagnostics;
using ComfySdk.Exceptions;
using ComfySdk.Http;
using ComfySdk.Models;
using ComfySdk.Options;
using Microsoft.Extensions.Logging.Abstractions;

await RunMaskingTestAsync();
await RunRetry429TestAsync();
await RunNoRetry400TestAsync();
await RunWsReconnectAndTerminalTestAsync();
await RunWsCancelStopsWithoutInterruptTestAsync();
await RunDownloadRedirectTestAsync();
await RunSubmitAndHistoryMappingTestAsync();
await RunSubmitAndHistoryObjectMappingTestAsync();

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

static async Task RunWsReconnectAndTerminalTestAsync()
{
    var options = new ComfyClientOptions
    {
        BaseUrl = new Uri("http://localhost:8188"),
        EnableWsReconnect = true,
        WsMaxReconnectAttempts = 2,
        WsReconnectBaseDelay = TimeSpan.FromMilliseconds(1),
    };

    var client = new ComfyClient(options);
    var events = new List<RunEventType>();

    await foreach (var runEvent in client.RunStreamAsync(new { Prompt = "cat" }))
    {
        events.Add(runEvent.Type);
    }

    Assert(events.Contains(RunEventType.Disconnected), "expected Disconnected event in stream");
    Assert(events.Contains(RunEventType.Reconnected), "expected Reconnected event in stream");
    var last = events[^1];
    Assert(last is RunEventType.Succeeded or RunEventType.Failed, "expected terminal event in stream");
}

static async Task RunWsCancelStopsWithoutInterruptTestAsync()
{
    var options = new ComfyClientOptions
    {
        BaseUrl = new Uri("http://localhost:8188"),
        EnableWsReconnect = true,
        WsMaxReconnectAttempts = 2,
        WsReconnectBaseDelay = TimeSpan.FromMilliseconds(10),
    };

    var client = new ComfyClient(options);
    using var cts = new CancellationTokenSource();
    cts.CancelAfter(20);

    var events = new List<RunEventType>();
    await foreach (var runEvent in client.RunStreamAsync(new { Prompt = "cat" }, cts.Token))
    {
        events.Add(runEvent.Type);
    }

    Assert(events.Count > 0, "expected at least one event before cancellation");
    Assert(!events.Contains(RunEventType.Succeeded), "did not expect success terminal on canceled stream");
}

static async Task RunDownloadRedirectTestAsync()
{
    var redirected = new HttpResponseMessage(HttpStatusCode.Found);
    redirected.Headers.Location = new Uri("/cdn/output.png", UriKind.Relative);

    var final = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 }),
    };

    var handler = new SequenceHandler(redirected, final);
    using var httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri("https://api.comfy.org"),
    };

    var options = new ComfyClientOptions
    {
        BaseUrl = new Uri("https://api.comfy.org"),
    };

    var transport = new ComfyHttpClient(httpClient, options, NullLogger<ComfyHttpClient>.Instance);
    var client = new ComfyClient(options, transport, NullLogger<ComfyClient>.Instance);

    var bytes = await client.DownloadAsync(new ViewParams("https://api.comfy.org/view?file=output.png"), "prompt-redirect");
    Assert(handler.AttemptCount == 2, $"expected 2 download attempts (redirect+final), got {handler.AttemptCount}");
    Assert(bytes.Length == 4, $"expected 4 bytes, got {bytes.Length}");
}

static async Task RunSubmitAndHistoryMappingTestAsync()
{
    var submit = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("{\"prompt_id\":\"prompt-123\"}"),
    };

    var history = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("{\"outputs\":[{\"name\":\"image_1\",\"type\":\"image\",\"url\":\"/view?filename=image_1.png\"}]}"),
    };

    var handler = new SequenceHandler(submit, history);
    using var httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri("https://api.comfy.org"),
    };

    var options = new ComfyClientOptions
    {
        BaseUrl = new Uri("https://api.comfy.org"),
        ApiPrefix = "/api",
        RouteMap = new ComfySdk.Routing.RouteMap(
            SubmitPrompt: "/prompt",
            HistoryV2: "/history_v2"),
    };

    var transport = new ComfyHttpClient(httpClient, options, NullLogger<ComfyHttpClient>.Instance);
    var client = new ComfyClient(options, transport, NullLogger<ComfyClient>.Instance);

    var promptId = await client.SubmitAsync("{}", CancellationToken.None);
    Assert(promptId == "prompt-123", $"expected promptId prompt-123, got {promptId}");

    var outputs = await client.GetHistoryAsync(promptId, CancellationToken.None);
    Assert(outputs.Count == 1, $"expected 1 output, got {outputs.Count}");
    Assert(outputs[0].Type == "image", $"expected image type, got {outputs[0].Type}");
    Assert(outputs[0].Url?.ToString().Contains("/view?filename=image_1.png", StringComparison.Ordinal) == true, "expected mapped output URL");
}

static async Task RunSubmitAndHistoryObjectMappingTestAsync()
{
    var submit = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("{\"prompt_id\":\"prompt-obj\"}"),
    };

    var history = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("""
{
  "prompt-obj": {
    "outputs": {
      "9": {
        "images": [
          {
            "filename": "ComfyUI_00001_.png",
            "subfolder": "",
            "type": "output"
          }
        ]
      }
    }
  }
}
"""),
    };

    var handler = new SequenceHandler(submit, history);
    using var httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri("http://127.0.0.1:8188"),
    };

    var options = new ComfyClientOptions
    {
        BaseUrl = new Uri("http://127.0.0.1:8188"),
        RouteMap = new ComfySdk.Routing.RouteMap(
            SubmitPrompt: "/prompt",
            HistoryV2: "/history_v2",
            HistoryV1: "/history",
            View: "/view"),
    };

    var transport = new ComfyHttpClient(httpClient, options, NullLogger<ComfyHttpClient>.Instance);
    var client = new ComfyClient(options, transport, NullLogger<ComfyClient>.Instance);

    var promptId = await client.SubmitAsync("{}", CancellationToken.None);
    Assert(promptId == "prompt-obj", $"expected promptId prompt-obj, got {promptId}");

    var outputs = await client.GetHistoryAsync(promptId, CancellationToken.None);
    Assert(outputs.Count == 1, $"expected 1 output, got {outputs.Count}");
    Assert(outputs[0].Type == "image", $"expected image type, got {outputs[0].Type}");
    Assert(outputs[0].Url?.ToString().Contains("/view?", StringComparison.Ordinal) == true, "expected /view URL");
    Assert(outputs[0].Url?.ToString().Contains("filename=ComfyUI_00001_.png", StringComparison.Ordinal) == true, "expected filename in URL");
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
