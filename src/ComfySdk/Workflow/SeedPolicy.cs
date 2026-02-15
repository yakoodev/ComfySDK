namespace ComfySdk.Workflow;

public enum SeedPolicyKind
{
    Fixed,
    Random,
    FromValue
}

public sealed record SeedPolicy
{
    public required SeedPolicyKind Kind { get; init; }

    public long? FixedSeed { get; init; }

    public long Resolve(long? fromValue, Func<long> randomSeedFactory)
    {
        return Kind switch
        {
            SeedPolicyKind.Fixed => FixedSeed
                ?? throw new InvalidOperationException("SeedPolicy.Fixed requires FixedSeed."),
            SeedPolicyKind.Random => randomSeedFactory(),
            SeedPolicyKind.FromValue => fromValue
                ?? throw new InvalidOperationException("SeedPolicy.FromValue requires a runtime value."),
            _ => throw new InvalidOperationException($"Unsupported seed policy: {Kind}.")
        };
    }
}
