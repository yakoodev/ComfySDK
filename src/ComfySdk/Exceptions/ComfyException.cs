namespace ComfySdk.Exceptions;

/// <summary>Unified exception for Comfy HTTP/runtime failures.</summary>
public sealed class ComfyException : Exception
{
    /// <summary>HTTP status code if available.</summary>
    public int? HttpStatus { get; }

    /// <summary>Route path used for request.</summary>
    public string Route { get; }

    /// <summary>Server request identifier if available.</summary>
    public string? RequestId { get; }

    /// <summary>Prompt identifier related to operation if available.</summary>
    public string? PromptId { get; }

    /// <summary>Small response body excerpt for diagnostics.</summary>
    public string? BodySnippet { get; }

    /// <summary>Creates Comfy exception.</summary>
    public ComfyException(
        string message,
        string route,
        int? httpStatus = null,
        string? requestId = null,
        string? promptId = null,
        string? bodySnippet = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Route = route;
        HttpStatus = httpStatus;
        RequestId = requestId;
        PromptId = promptId;
        BodySnippet = bodySnippet;
    }
}
