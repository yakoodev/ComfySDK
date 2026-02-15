using System.Text.Json.Nodes;
using ComfySdk.Settings;
using ComfySdk.Workflow;

await RunMaterializationSmokeAsync();

Console.WriteLine("ComfySdk.SmokeTests: PASS");
return 0;

static async Task RunMaterializationSmokeAsync()
{
    const string workflowJson = """
{
  "20": {
    "class_type": "CLIPTextEncode",
    "inputs": { "text": "old prompt" }
  }
}
""";

    var settings = new SettingsSpec
    {
        SettingsSchemaVersion = 1,
        Parameters =
        [
            new SettingsParameterSpec
            {
                Name = "prompt",
                Type = "string",
                Selector = new NodeSelector { NodeId = "20" },
                Path = "inputs.text",
                Required = true,
            }
        ]
    };

    var materializer = new WorkflowMaterializer();
    var request = new ParameterizedWorkflowMaterializationRequest(
        Parameters: new SmokeParams { Prompt = "smoke prompt" },
        Settings: settings);

    var result = await materializer.MaterializeAsync(
        WorkflowTemplate.Parse(workflowJson),
        request);

    var root = JsonNode.Parse(result.WorkflowJson)!.AsObject();
    var prompt = root["20"]!["inputs"]!["text"]!.GetValue<string>();

    Ensure(prompt == "smoke prompt", $"Expected prompt 'smoke prompt', got '{prompt}'.");
}

static void Ensure(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

file sealed record SmokeParams
{
    public string Prompt { get; init; } = string.Empty;
}
