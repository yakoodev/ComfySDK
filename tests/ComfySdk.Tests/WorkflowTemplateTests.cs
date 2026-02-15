using System.Text.Json.Nodes;
using ComfySdk.Workflow;

namespace ComfySdk.Tests;

public static class WorkflowTemplateTests
{
    public static void Selector_MatchingMultipleNodes_ShouldThrow()
    {
        const string workflowJson = """
{
  "10": { "class_type": "KSampler", "inputs": { "seed": 1 } },
  "11": { "class_type": "KSampler", "inputs": { "seed": 2 } }
}
""";

        var template = WorkflowTemplate.Parse(workflowJson);
        var patch = new WorkflowPatch(
            new NodeSelector
            {
                ClassType = "KSampler"
            },
            "inputs.seed",
            JsonValue.Create(123));

        var thrown = false;
        try
        {
            template.ApplyPatch(patch);
        }
        catch (InvalidOperationException ex)
        {
            thrown = ex.Message.Contains("exactly one node", StringComparison.OrdinalIgnoreCase);
        }

        Ensure(thrown, "Expected InvalidOperationException for selector with != 1 matches.");
    }

    public static void Patch_WithArrayPath_ShouldUpdateJson()
    {
        const string workflowJson = """
{
  "20": {
    "class_type": "NodeWithArray",
    "inputs": {
      "items": [ { "value": 1 }, { "value": 2 } ]
    }
  }
}
""";

        var template = WorkflowTemplate.Parse(workflowJson);
        var patch = new WorkflowPatch(
            new NodeSelector
            {
                NodeId = "20"
            },
            "inputs.items[1].value",
            JsonValue.Create(42));

        template.ApplyPatch(patch);

        var root = JsonNode.Parse(template.ToJson())!.AsObject();
        var value = root["20"]!["inputs"]!["items"]![1]!["value"]!.GetValue<int>();
        Ensure(value == 42, $"Expected patched value 42, got {value}.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
