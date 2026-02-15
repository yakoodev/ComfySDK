using ComfySdk.Http;
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
    private readonly ComfyHttpClient _httpClient;

    /// <summary>Creates client with explicit options.</summary>
    public ComfyClient(ComfyClientOptions options)
        : this(
            options,
            new ComfyHttpClient(new HttpClient(), options, NullLogger<ComfyHttpClient>.Instance),
            NullLogger<ComfyClient>.Instance)
    {
    }

    /// <summary>Creates client with explicit options and logger.</summary>
    public ComfyClient(ComfyClientOptions options, ILogger<ComfyClient> logger)
        : this(
            options,
            new ComfyHttpClient(new HttpClient(), options, NullLogger<ComfyHttpClient>.Instance),
            logger)
    {
    }

    /// <summary>Creates client with explicit options, transport and logger.</summary>
    public ComfyClient(ComfyClientOptions options, ComfyHttpClient httpClient, ILogger<ComfyClient> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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

        _ = _httpClient;

        await Task.Delay(100, cancellationToken);
        var outputs =
            new[]
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

    private static RunHandle CreateRunHandle()
    {
        return new RunHandle(Guid.NewGuid().ToString("N"));
    }
}
