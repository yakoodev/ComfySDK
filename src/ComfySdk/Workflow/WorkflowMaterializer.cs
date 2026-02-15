using System.Text.Json.Nodes;

namespace ComfySdk.Workflow;

public sealed record SeedTarget(NodeSelector Selector, string InputName = "seed");

public sealed record SeedApplication(SeedTarget Target, SeedPolicy Policy, long? Value = null);

public sealed record WorkflowMaterializationRequest(
    IReadOnlyList<WorkflowPatch>? Patches = null,
    SeedApplication? Seed = null,
    IReadOnlyList<WorkflowPatch>? Overrides = null);

public sealed record WorkflowMaterializationResult(string WorkflowJson, long? AppliedSeed);

public sealed class WorkflowMaterializer
{
    private readonly Func<long> _randomSeedFactory;

    public WorkflowMaterializer(Func<long>? randomSeedFactory = null)
    {
        _randomSeedFactory = randomSeedFactory ?? (() => Random.Shared.NextInt64(0, long.MaxValue));
    }

    public WorkflowMaterializationResult Materialize(
        WorkflowTemplate template,
        WorkflowMaterializationRequest? request = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        var working = WorkflowTemplate.Parse(template.RawJson);
        var patches = request?.Patches ?? [];
        var overrides = request?.Overrides ?? [];

        foreach (var patch in patches)
        {
            working.ApplyPatch(patch);
        }

        long? appliedSeed = null;
        if (request?.Seed is not null)
        {
            appliedSeed = request.Seed.Policy.Resolve(request.Seed.Value, _randomSeedFactory);
            var seedPatch = new WorkflowPatch(
                request.Seed.Target.Selector,
                $"inputs.{request.Seed.Target.InputName}",
                JsonValue.Create(appliedSeed.Value));
            working.ApplyPatch(seedPatch);
        }

        foreach (var patch in overrides)
        {
            working.ApplyPatch(patch);
        }

        return new WorkflowMaterializationResult(working.ToJson(), appliedSeed);
    }
}
