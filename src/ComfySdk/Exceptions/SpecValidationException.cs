namespace ComfySdk.Exceptions;

/// <summary>Thrown when settings/workflow specification is invalid.</summary>
public sealed class SpecValidationException : ComfyException
{
    public SpecValidationException(string message)
        : base(message)
    {
    }

    public SpecValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
