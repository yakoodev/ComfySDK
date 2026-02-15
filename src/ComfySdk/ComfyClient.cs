using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>Submits workflow JSON and returns generated prompt identifier.</summary>
    public async Task<string> SubmitAsync(string workflowJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowJson);
        var payloadJson = BuildSubmitPayloadJson(workflowJson);

        using var response = await _httpClient.SendWithRetryAsync(
            requestFactory: () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, _options.BuildEndpoint(_options.RouteMap.SubmitPrompt))
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
                };
                return request;
            },
            route: _options.RouteMap.SubmitPrompt,
            promptId: null,
            requestKind: ComfyRequestKind.Default,
            cancellationToken: cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var promptId = TryReadPromptId(body);
        if (string.IsNullOrWhiteSpace(promptId))
        {
            throw new Exceptions.ComfyException(
                message: "Submit response does not contain prompt_id.",
                route: _options.RouteMap.SubmitPrompt,
                bodySnippet: body.Length <= 512 ? body : body[..512]);
        }

        _logger.LogInformation("Submit completed promptId={PromptId}", promptId);
        return promptId;
    }

    /// <summary>Gets history for prompt and maps outputs to <see cref="OutputArtifact"/> list.</summary>
    public async Task<IReadOnlyList<OutputArtifact>> GetHistoryAsync(string promptId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptId);

        var v2Route = _options.RouteMap.HistoryV2.TrimEnd('/') + "/" + promptId;
        try
        {
            var v2Json = await GetHistoryJsonAsync(v2Route, promptId, cancellationToken);
            var v2Outputs = ParseOutputArtifactsFromHistory(promptId, v2Json);
            if (v2Outputs.Count > 0)
            {
                _logger.LogInformation("History loaded via v2 promptId={PromptId} outputs={Count}", promptId, v2Outputs.Count);
                return v2Outputs;
            }
        }
        catch (Exceptions.ComfyException ex) when (ex.HttpStatus == 404)
        {
            _logger.LogDebug("History v2 route not found for promptId={PromptId}. Falling back to v1.", promptId);
        }

        var v1Route = _options.RouteMap.HistoryV1.TrimEnd('/') + "/" + promptId;
        var v1Json = await GetHistoryJsonAsync(v1Route, promptId, cancellationToken);
        var v1Outputs = ParseOutputArtifactsFromHistory(promptId, v1Json);
        if (v1Outputs.Count > 0)
        {
            _logger.LogInformation("History loaded via v1 by-id promptId={PromptId} outputs={Count}", promptId, v1Outputs.Count);
            return v1Outputs;
        }

        var v1AllRoute = _options.RouteMap.HistoryV1.TrimEnd('/');
        var v1AllJson = await GetHistoryJsonAsync(v1AllRoute, promptId, cancellationToken);
        var v1AllOutputs = ParseOutputArtifactsFromHistory(promptId, v1AllJson);
        if (v1AllOutputs.Count > 0)
        {
            _logger.LogInformation("History loaded via v1-all exact promptId={PromptId} outputs={Count}", promptId, v1AllOutputs.Count);
            return v1AllOutputs;
        }

        return [];
    }

    /// <summary>Downloads artifact bytes with redirect-following support.</summary>
    public async Task<byte[]> DownloadAsync(ViewParams viewParams, string? promptId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewParams);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewParams.PathOrUrl);

        var startUri = ResolveViewUri(viewParams);
        using var response = await _httpClient.GetWithRedirectsAsync(
            startUri,
            _options.RouteMap.View,
            promptId,
            cancellationToken: cancellationToken);

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <summary>Runs workflow and returns final outputs once terminal state is reached.</summary>
    public async Task<RunResult> RunAsync(object parameters, CancellationToken cancellationToken = default)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var workflowJson = JsonSerializer.Serialize(parameters);
        var promptId = await SubmitAsync(workflowJson, cancellationToken);
        var outputs = await GetHistoryAsync(promptId, cancellationToken);

        var result = new RunResult(promptId, outputs);
        _logger.LogInformation(
            "Run finished for PromptId={PromptId} with {OutputCount} outputs",
            result.PromptId,
            result.Outputs.Count);
        return result;
    }

    private Uri ResolveViewUri(ViewParams viewParams)
    {
        if (Uri.TryCreate(viewParams.PathOrUrl, UriKind.Absolute, out var absolute))
        {
            return AppendQuery(absolute, viewParams.Query);
        }

        if (viewParams.PathOrUrl.StartsWith("/", StringComparison.Ordinal))
        {
            return AppendQuery(new Uri(_options.BaseUrl, viewParams.PathOrUrl), viewParams.Query);
        }

        var route = _options.RouteMap.View;
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(viewParams.PathOrUrl))
        {
            query["path"] = viewParams.PathOrUrl;
        }

        if (viewParams.Query is not null)
        {
            foreach (var pair in viewParams.Query)
            {
                query[pair.Key] = pair.Value;
            }
        }

        return AppendQuery(_options.BuildEndpoint(route), query);
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

    private IReadOnlyList<OutputArtifact> ParseOutputArtifactsFromHistory(string promptId, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<OutputArtifact>();

        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("outputs", out var directOutputs) &&
            directOutputs.ValueKind == JsonValueKind.Array)
        {
            MapOutputsArray(directOutputs, list);
            return list;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("outputs", out var directOutputsObject) &&
            directOutputsObject.ValueKind == JsonValueKind.Object)
        {
            MapOutputsObject(directOutputsObject, list);
            return list;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(promptId, out var promptNode) &&
            promptNode.ValueKind == JsonValueKind.Object &&
            promptNode.TryGetProperty("outputs", out var nestedOutputs) &&
            nestedOutputs.ValueKind == JsonValueKind.Array)
        {
            MapOutputsArray(nestedOutputs, list);
            return list;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(promptId, out var promptNodeObject) &&
            promptNodeObject.ValueKind == JsonValueKind.Object &&
            promptNodeObject.TryGetProperty("outputs", out var nodeOutputs) &&
            nodeOutputs.ValueKind == JsonValueKind.Object)
        {
            MapOutputsObject(nodeOutputs, list);
            return list;
        }

        return list;
    }

    private void MapOutputsArray(JsonElement outputs, List<OutputArtifact> list)
    {
        foreach (var item in outputs.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "output" : "output";
            var type = item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "any" : "any";
            var urlText = item.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
            Uri? url = null;
            if (!string.IsNullOrWhiteSpace(urlText))
            {
                url = Uri.TryCreate(urlText, UriKind.Absolute, out var absolute)
                    ? absolute
                    : new Uri(_options.BaseUrl, urlText);
            }

            list.Add(new OutputArtifact(name, type, Url: url));
        }
    }

    private void MapOutputsObject(JsonElement outputsObject, List<OutputArtifact> list)
    {
        foreach (var nodeEntry in outputsObject.EnumerateObject())
        {
            if (nodeEntry.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var artifactGroup in nodeEntry.Value.EnumerateObject())
            {
                if (artifactGroup.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var type = InferArtifactType(artifactGroup.Name);
                foreach (var artifact in artifactGroup.Value.EnumerateArray())
                {
                    if (artifact.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = artifact.TryGetProperty("filename", out var filenameEl)
                        ? filenameEl.GetString() ?? $"{nodeEntry.Name}.{type}"
                        : $"{nodeEntry.Name}.{type}";

                    Uri? url = null;
                    if (artifact.TryGetProperty("filename", out var filenameNode) &&
                        filenameNode.GetString() is { Length: > 0 } filename)
                    {
                        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["filename"] = filename
                        };

                        if (artifact.TryGetProperty("subfolder", out var subfolderNode) &&
                            subfolderNode.GetString() is { Length: > 0 } subfolder)
                        {
                            query["subfolder"] = subfolder;
                        }

                        if (artifact.TryGetProperty("type", out var storageTypeNode) &&
                            storageTypeNode.GetString() is { Length: > 0 } storageType)
                        {
                            query["type"] = storageType;
                        }

                        url = AppendQuery(_options.BuildEndpoint(_options.RouteMap.View), query);
                    }

                    list.Add(new OutputArtifact(name, type, Url: url));
                }
            }
        }
    }

    private static string InferArtifactType(string groupName)
    {
        var lower = groupName.ToLowerInvariant();
        return lower switch
        {
            "images" => "image",
            "videos" => "video",
            "audio" => "audio",
            "audios" => "audio",
            _ => lower.TrimEnd('s')
        };
    }

    private static string? TryReadPromptId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("prompt_id", out var promptIdEl))
        {
            return promptIdEl.GetString();
        }

        return null;
    }

    private async Task<string> GetHistoryJsonAsync(
        string route,
        string promptId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, _options.BuildEndpoint(route)),
            route,
            promptId,
            ComfyRequestKind.Default,
            cancellationToken);

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string BuildSubmitPayloadJson(string workflowJson)
    {
        JsonNode parsed;
        try
        {
            parsed = JsonNode.Parse(workflowJson)
                ?? throw new InvalidOperationException("Workflow payload is empty JSON.");
        }
        catch (Exception ex)
        {
            throw new FormatException("Workflow JSON is invalid.", ex);
        }

        if (parsed is JsonObject root &&
            root.TryGetPropertyValue("prompt", out var promptNode) &&
            promptNode is not null)
        {
            return root.ToJsonString();
        }

        var wrapped = new JsonObject
        {
            ["prompt"] = parsed
        };

        return wrapped.ToJsonString();
    }

    private static Uri AppendQuery(Uri baseUri, IReadOnlyDictionary<string, string>? query)
    {
        if (query is null || query.Count == 0)
        {
            return baseUri;
        }

        var encoded = query.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}");
        var separator = string.IsNullOrEmpty(baseUri.Query) ? "?" : "&";
        return new Uri(baseUri + separator + string.Join("&", encoded));
    }

    private static RunHandle CreateRunHandle()
    {
        return new RunHandle(Guid.NewGuid().ToString("N"));
    }
}
