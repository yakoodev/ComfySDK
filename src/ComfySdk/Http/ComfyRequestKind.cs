namespace ComfySdk.Http;

/// <summary>Logical request kind for timeout selection.</summary>
public enum ComfyRequestKind
{
    /// <summary>Default API request timeout.</summary>
    Default,

    /// <summary>Upload request timeout.</summary>
    Upload,

    /// <summary>Download request timeout.</summary>
    Download,
}
