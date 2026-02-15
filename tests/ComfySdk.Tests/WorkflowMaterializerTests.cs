using System.Text.Json.Nodes;
using ComfySdk.Workflow;

namespace ComfySdk.Tests;

public static class WorkflowMaterializerTests
{
    public static void RandomSeed_IsGeneratedPerRun()
    {
        const string workflowJson = """
{
  "10": { "class_type": "KSampler", "inputs": { "seed": 1 } }
}
""";

        long next = 1000;
        var materializer = new WorkflowMaterializer(() => ++next);
        var template = WorkflowTemplate.Parse(workflowJson);
        var request = new WorkflowMaterializationRequest(
            Seed: new SeedApplication(
                new SeedTarget(new NodeSelector { NodeId = "10" }),
                new SeedPolicy { Kind = SeedPolicyKind.Random }));

        var run1 = materializer.Materialize(template, request);
        var run2 = materializer.Materialize(template, request);

        Ensure(run1.AppliedSeed.HasValue, "Expected first run to produce seed.");
        Ensure(run2.AppliedSeed.HasValue, "Expected second run to produce seed.");
        var seed1 = run1.AppliedSeed.GetValueOrDefault();
        var seed2 = run2.AppliedSeed.GetValueOrDefault();
        Ensure(seed1 != seed2, "Expected different seeds between runs.");
    }

    public static void Overrides_AreAppliedAfterSeedAndPatches()
    {
        const string workflowJson = """
{
  "10": { "class_type": "KSampler", "inputs": { "seed": 1, "cfg": 3.0 } }
}
""";

        var materializer = new WorkflowMaterializer(() => 222);
        var template = WorkflowTemplate.Parse(workflowJson);

        var request = new WorkflowMaterializationRequest(
            Patches:
            [
                new WorkflowPatch(
                    new NodeSelector { NodeId = "10" },
                    "inputs.seed",
                    JsonValue.Create(11))
            ],
            Seed: new SeedApplication(
                new SeedTarget(new NodeSelector { NodeId = "10" }),
                new SeedPolicy { Kind = SeedPolicyKind.Fixed, FixedSeed = 123 }),
            Overrides:
            [
                new WorkflowPatch(
                    new NodeSelector { NodeId = "10" },
                    "inputs.seed",
                    JsonValue.Create(999))
            ]);

        var result = materializer.Materialize(template, request);
        var root = JsonNode.Parse(result.WorkflowJson)!.AsObject();
        var finalSeed = root["10"]!["inputs"]!["seed"]!.GetValue<int>();

        Ensure(finalSeed == 999, $"Expected override seed 999, got {finalSeed}.");
        Ensure(result.AppliedSeed == 123, $"Expected applied seed diagnostics to be 123, got {result.AppliedSeed}.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
