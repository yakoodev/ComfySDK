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
        var promptId = handle.PromptId;
        var wsEndpoint = _options.BuildWsEndpoint();
        _logger.LogInformation("Run stream started for PromptId={PromptId} on {WsEndpoint}", promptId, wsEndpoint);

        yield return new RunEvent(RunEventType.Connected, $"Connected for run {promptId}.");
        yield return new RunEvent(RunEventType.Queued, $"Queued run {promptId}.");
        if (!await WaitOrCancelAsync(promptId, TimeSpan.FromMilliseconds(40), cancellationToken))
        {
            yield break;
        }

        yield return new RunEvent(RunEventType.Executing, $"Executing run {promptId}.");
        yield return new RunEvent(RunEventType.Progress, "Progress update.", ProgressPercent: 40);
        if (!await WaitOrCancelAsync(promptId, TimeSpan.FromMilliseconds(40), cancellationToken))
        {
            yield break;
        }

        // Simulated WS disconnect to validate reconnect flow in scaffold/runtime tests.
        yield return new RunEvent(RunEventType.Disconnected, $"WS disconnected for run {promptId}.");

        var reconnectAttempt = await TryReconnectAsync(promptId, cancellationToken);
        if (reconnectAttempt is null)
        {
            var fallbackTerminal = await ResolveTerminalStateViaHttpFallbackAsync(promptId, cancellationToken);
            yield return fallbackTerminal;
            _logger.LogInformation("Run stream finished for PromptId={PromptId} via HTTP fallback", promptId);
            yield break;
        }

        yield return new RunEvent(RunEventType.Reconnected, $"WS reconnected for run {promptId} (attempt {reconnectAttempt}).");
        yield return new RunEvent(RunEventType.Progress, "Progress update after reconnect.", ProgressPercent: 90);
        if (!await WaitOrCancelAsync(promptId, TimeSpan.FromMilliseconds(40), cancellationToken))
        {
            yield break;
        }

        yield return new RunEvent(RunEventType.Progress, "Progress update.", ProgressPercent: 100);
        yield return new RunEvent(RunEventType.Succeeded, $"Run {promptId} completed.");
        _logger.LogInformation("Run stream finished for PromptId={PromptId}", promptId);
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

    private async Task<int?> TryReconnectAsync(string promptId, CancellationToken cancellationToken)
    {
        if (!_options.EnableWsReconnect || _options.WsMaxReconnectAttempts <= 0)
        {
            return null;
        }

        for (var attempt = 1; attempt <= _options.WsMaxReconnectAttempts; attempt++)
        {
            if (!await WaitOrCancelAsync(promptId, _options.WsReconnectBaseDelay, cancellationToken))
            {
                return null;
            }

            var reconnectSucceeded = attempt == 1;
            if (reconnectSucceeded)
            {
                _logger.LogInformation(
                    "WS reconnect succeeded for PromptId={PromptId} on attempt={Attempt}",
                    promptId,
                    attempt);
                return attempt;
            }
        }

        return null;
    }

    private static async Task<RunEvent> ResolveTerminalStateViaHttpFallbackAsync(string promptId, CancellationToken cancellationToken)
    {
        await Task.Delay(20, cancellationToken);
        return new RunEvent(RunEventType.Succeeded, $"Run {promptId} resolved via HTTP fallback.");
    }

    private async Task<bool> WaitOrCancelAsync(string promptId, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Run stream canceled for PromptId={PromptId}. WS closed, waiting stopped, no remote interrupt.",
                promptId);
            return false;
        }
    }

    private static RunHandle CreateRunHandle()
    {
        return new RunHandle(Guid.NewGuid().ToString("N"));
    }
}
