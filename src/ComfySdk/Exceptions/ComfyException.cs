namespace ComfySdk.Exceptions;

/// <summary>Base SDK exception with optional transport/runtime context.</summary>
public class ComfyException : Exception
{
    public ComfyException(string message)
        : base(message)
    {
    }

    public ComfyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public int? HttpStatus { get; init; }

    public string? Route { get; init; }

    public string? RequestId { get; init; }

    public string? PromptId { get; init; }

    public string? BodySnippet { get; init; }

    public IReadOnlyList<string> NodeErrors { get; init; } = [];
}
