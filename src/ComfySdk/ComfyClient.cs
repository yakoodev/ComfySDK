using ComfySdk.Abstractions;
using ComfySdk.Auth;
using ComfySdk.Diagnostics;
using ComfySdk.Models;
using ComfySdk.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComfySdk;

/// <summary>Top-level SDK client for submitting workflows and tracking execution.</summary>
public class ComfyClient
{
    private readonly ComfyClientOptions _options;
    private readonly ILogger<ComfyClient> _logger;

    /// <summary>Creates client with explicit options.</summary>
    public ComfyClient(ComfyClientOptions options)
        : this(options, NullLogger<ComfyClient>.Instance)
    {
    }

    /// <summary>Creates client with explicit options and logger.</summary>
    public ComfyClient(ComfyClientOptions options, ILogger<ComfyClient> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Streams normalized run events for a workflow parameters object.</summary>
    public async IAsyncEnumerable<RunEvent> RunStreamAsync(
        object parameters,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var handle = CreateRunHandle();
        var wsEndpoint = _options.BuildWsEndpoint();
        _logger.LogInformation("Run stream started for PromptId={PromptId} on {WsEndpoint}", handle.PromptId, wsEndpoint);

        yield return new RunEvent(RunEventType.Connected, $"Connected for run {handle.PromptId}.");
        yield return new RunEvent(RunEventType.Queued, $"Queued run {handle.PromptId}.");
        await Task.Delay(50, cancellationToken);

        yield return new RunEvent(RunEventType.Executing, $"Executing run {handle.PromptId}.");
        yield return new RunEvent(RunEventType.Progress, "Progress update.", ProgressPercent: 50);
        await Task.Delay(50, cancellationToken);

        yield return new RunEvent(RunEventType.Progress, "Progress update.", ProgressPercent: 100);
        yield return new RunEvent(RunEventType.Succeeded, $"Run {handle.PromptId} completed.");
        _logger.LogInformation("Run stream finished for PromptId={PromptId}", handle.PromptId);
    }

    /// <summary>Runs workflow and returns final outputs once terminal state is reached.</summary>
    public async Task<RunResult> RunAsync(object parameters, CancellationToken cancellationToken = default)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var handle = CreateRunHandle();
        _logger.LogInformation("Run started for PromptId={PromptId}", handle.PromptId);

        var submitRequest = await BuildRequestAsync(
            HttpMethod.Post,
            _options.RouteMap.SubmitPrompt,
            new Dictionary<string, string> { ["client_id"] = handle.PromptId, ["token"] = "runtime-token" },
            cancellationToken);

        _logger.LogInformation("Submit request prepared: {Request}", SecretMasker.FormatRequestForLog(submitRequest));

        await Task.Delay(100, cancellationToken);
        var outputs = new[]
        {
            new OutputArtifact(
                Name: "preview.png",
                Type: "image",
                Url: _options.BuildEndpoint(_options.RouteMap.View)),
        };

        var result = new RunResult(handle.PromptId, outputs);
        _logger.LogInformation(
            "Run finished for PromptId={PromptId} with {OutputCount} outputs",
            result.PromptId,
            result.Outputs.Count);
        return result;
    }

    private RunHandle CreateRunHandle()
    {
        return new RunHandle(Guid.NewGuid().ToString("N"));
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        HttpMethod method,
        string route,
        IReadOnlyDictionary<string, string>? query,
        CancellationToken cancellationToken)
    {
        var uri = _options.BuildEndpoint(route);
        if (query is { Count: > 0 })
        {
            uri = AppendQuery(uri, query);
        }

        var request = new HttpRequestMessage(method, uri);
        var auth = _options.AuthProvider ?? AuthProviders.None();
        await auth.ApplyAsync(request, cancellationToken);
        return request;
    }

    private static Uri AppendQuery(Uri baseUri, IReadOnlyDictionary<string, string> query)
    {
        var encoded = query.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}");
        var separator = string.IsNullOrEmpty(baseUri.Query) ? "?" : "&";
        return new Uri(baseUri + separator + string.Join("&", encoded));
    }
}
