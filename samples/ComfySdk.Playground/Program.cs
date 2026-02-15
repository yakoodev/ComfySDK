using ComfySdk;
using ComfySdk.Exceptions;
using ComfySdk.Generated;
using ComfySdk.Models;
using ComfySdk.Options;
using ComfySdk.Settings;
using ComfySdk.Workflow;
using System.Text.Json;

var baseUrlRaw = Environment.GetEnvironmentVariable("COMFY_BASE_URL") ?? "http://localhost:8188";
if (!Uri.TryCreate(baseUrlRaw, UriKind.Absolute, out var baseUrl))
{
    Console.WriteLine($"Invalid COMFY_BASE_URL: {baseUrlRaw}");
    return;
}

var submitEnabled = ParseBoolean(Environment.GetEnvironmentVariable("COMFY_SUBMIT"), defaultValue: true);
var waitSeconds = ParseInt(Environment.GetEnvironmentVariable("COMFY_WAIT_SECONDS"), defaultValue: 90);
var prompt = Environment.GetEnvironmentVariable("COMFY_PROMPT")
    ?? "china woman";
var seed = ParseLong(Environment.GetEnvironmentVariable("COMFY_SEED"), defaultValue: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

var projectDir = ResolveProjectDirectory();
var specsDir = Path.Combine(projectDir, "Specs", "Default");
var workflowPath = Path.Combine(specsDir, "default.json");
var settingsPath = Path.Combine(specsDir, "settings.default.json");
if (!File.Exists(workflowPath))
{
    Console.WriteLine($"Workflow not found: {workflowPath}");
    return;
}

if (!File.Exists(settingsPath))
{
    Console.WriteLine($"Settings not found: {settingsPath}");
    return;
}

var connector = new ComfyUiConnector(baseUrl, workflowPath, settingsPath);
var request = new DefaultWorkflow
{
    PositivePrompt = prompt,
    Seed = seed,
    Steps = 24,
    Cfg = 8.0
};

var materialized = await connector.MaterializeAsync(request);
PrintMaterializedSummary(workflowPath, settingsPath, materialized.WorkflowJson);
Console.WriteLine($"Materialized from: {Path.GetFileName(workflowPath)}");
if (!string.IsNullOrWhiteSpace(materialized.DiagnosticsWorkflowPath))
{
    Console.WriteLine($"Diagnostics: {materialized.DiagnosticsWorkflowPath}");
}

if (!submitEnabled)
{
    Console.WriteLine("Set COMFY_SUBMIT=true to submit.");
    return;
}

Console.WriteLine("Submitting workflow and waiting for outputs...");
try
{
    var result = await connector.SubmitAndWaitHistoryAsync(
        materialized.WorkflowJson,
        TimeSpan.FromSeconds(waitSeconds));
    Console.WriteLine($"PromptId: {result.PromptId}");
    Console.WriteLine($"Outputs: {result.Outputs.Count}");
    if (result.Outputs.Count == 0)
    {
        Console.WriteLine("Run completed, but Comfy history returned empty outputs (likely fully cached execution).");
    }
    foreach (var output in result.Outputs)
    {
        Console.WriteLine($"  - {output.Type} {output.Name} {output.Url}");
    }
}
catch (ComfyException ex)
{
    Console.WriteLine($"Comfy error: HTTP={ex.HttpStatus} route={ex.Route} requestId={ex.RequestId}");
    if (!string.IsNullOrWhiteSpace(ex.BodySnippet))
    {
        Console.WriteLine("Server response:");
        Console.WriteLine(ex.BodySnippet);
    }
    throw;
}

static string ResolveProjectDirectory()
{
    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
}

static bool ParseBoolean(string? value, bool defaultValue)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return defaultValue;
    }

    return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("yes", StringComparison.OrdinalIgnoreCase);
}

static int ParseInt(string? value, int defaultValue)
{
    return int.TryParse(value, out var parsed) && parsed > 0
        ? parsed
        : defaultValue;
}

static long ParseLong(string? value, long defaultValue)
{
    return long.TryParse(value, out var parsed) && parsed >= 0
        ? parsed
        : defaultValue;
}

static void PrintMaterializedSummary(string workflowPath, string settingsPath, string workflowJson)
{
    Console.WriteLine($"Workflow path: {workflowPath}");
    Console.WriteLine($"Settings path: {settingsPath}");

    using var doc = JsonDocument.Parse(workflowJson);
    var root = doc.RootElement;

    var positive = TryReadNodeInput(root, "6", "text");
    var steps = TryReadNodeInput(root, "3", "steps");
    var cfg = TryReadNodeInput(root, "3", "cfg");
    var currentSeed = TryReadNodeInput(root, "3", "seed");
    Console.WriteLine($"Applied values: node6.text='{positive}', node3.steps='{steps}', node3.cfg='{cfg}', node3.seed='{currentSeed}'");
}

static string? TryReadNodeInput(JsonElement root, string nodeId, string inputName)
{
    if (root.ValueKind != JsonValueKind.Object)
    {
        return null;
    }

    if (!root.TryGetProperty(nodeId, out var node) || node.ValueKind != JsonValueKind.Object)
    {
        return null;
    }

    if (!node.TryGetProperty("inputs", out var inputs) || inputs.ValueKind != JsonValueKind.Object)
    {
        return null;
    }

    if (!inputs.TryGetProperty(inputName, out var value))
    {
        return null;
    }

    return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
}

file sealed class ComfyUiConnector
{
    private readonly Uri _baseUrl;
    private readonly string _workflowPath;
    private readonly SettingsSpec _settings;
    private readonly WorkflowMaterializer _materializer = new();
    private readonly ComfyClient _client;

    public ComfyUiConnector(Uri baseUrl, string workflowPath, string settingsPath)
    {
        _baseUrl = baseUrl;
        _workflowPath = workflowPath;
        _settings = SettingsSpecParser.ParseFile(settingsPath);
        _client = new ComfyClient(new ComfyClientOptions { BaseUrl = baseUrl });
    }

    public async ValueTask<WorkflowMaterializationResult> MaterializeAsync<TParameters>(TParameters parameters)
        where TParameters : class
    {
        var workflowJson = await File.ReadAllTextAsync(_workflowPath);
        return await _materializer.MaterializeAsync(
            WorkflowTemplate.Parse(workflowJson),
            new ParameterizedWorkflowMaterializationRequest(
                Parameters: parameters,
                Settings: _settings));
    }

    public async ValueTask<RunResult> SubmitAndWaitHistoryAsync(
        string materializedWorkflowJson,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var promptId = await _client.SubmitAsync(materializedWorkflowJson);
        var startedAt = DateTime.UtcNow;
        IReadOnlyList<OutputArtifact> outputs = [];

        while (DateTime.UtcNow - startedAt < timeout)
        {
            outputs = await _client.GetHistoryAsync(promptId, cancellationToken);
            if (outputs.Count > 0)
            {
                return new RunResult(promptId, outputs);
            }

            if (await IsPromptCompletedAsync(promptId, cancellationToken))
            {
                // Some Comfy builds mark run as completed before outputs are visible in history.
                var graceDeadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < graceDeadline)
                {
                    outputs = await _client.GetHistoryAsync(promptId, cancellationToken);
                    if (outputs.Count > 0)
                    {
                        return new RunResult(promptId, outputs);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }

                return new RunResult(promptId, outputs);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException(
            $"No outputs were returned for prompt '{promptId}' within {timeout.TotalSeconds:F0} seconds.");
    }

    private async ValueTask<bool> IsPromptCompletedAsync(string promptId, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { BaseAddress = _baseUrl };
        var response = await httpClient.GetAsync($"/history/{promptId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (TryReadCompleted(root, out var completed))
        {
            return completed;
        }

        if (root.TryGetProperty(promptId, out var promptEntry) &&
            promptEntry.ValueKind == JsonValueKind.Object &&
            TryReadCompleted(promptEntry, out completed))
        {
            return completed;
        }

        return false;
    }

    private static bool TryReadCompleted(JsonElement container, out bool completed)
    {
        completed = false;
        if (!container.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!status.TryGetProperty("completed", out var completedNode) ||
            completedNode.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            return false;
        }

        completed = completedNode.GetBoolean();
        return true;
    }
}
