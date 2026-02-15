namespace ComfySdk.Generated;

/// <summary>Generated parameters from default.json + settings.default.json.</summary>
public sealed class DefaultWorkflow
{
    /// <summary>Positive prompt for generation.</summary>
    public required string PositivePrompt { get; init; }

    /// <summary>Negative prompt.</summary>
    public string? NegativePrompt { get; init; } = @"text, watermark";

    /// <summary>Sampler seed.</summary>
    public long? Seed { get; init; } = 725769310524473L;

    /// <summary>Sampling steps.</summary>
    public int? Steps { get; init; } = 20;

    /// <summary>CFG scale.</summary>
    public double? Cfg { get; init; } = 8;

}
