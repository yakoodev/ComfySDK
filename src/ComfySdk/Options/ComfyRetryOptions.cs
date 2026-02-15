namespace ComfySdk.Options;

/// <summary>Configuration options for retry behavior.</summary>
public sealed class ComfyRetryOptions
{
    /// <summary>Maximum retry attempts after the first request.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Base delay used for exponential backoff.</summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Max additional jitter delay.</summary>
    public TimeSpan MaxJitter { get; init; } = TimeSpan.FromMilliseconds(150);
}
