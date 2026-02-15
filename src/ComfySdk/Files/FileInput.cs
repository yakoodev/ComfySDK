namespace ComfySdk.Files;

/// <summary>Represents a user-provided file source.</summary>
public abstract record FileInput
{
    /// <summary>Creates file input from local file path.</summary>
    public static FileInput FromPath(string path) => new PathFileInput(path);

    /// <summary>Creates file input from remote URL.</summary>
    public static FileInput FromUrl(Uri url) => new UrlFileInput(url);

    /// <summary>Creates file input from base64 payload.</summary>
    public static FileInput FromBase64(string base64) => new Base64FileInput(base64);

    /// <summary>Creates file input from byte array payload.</summary>
    public static FileInput FromBytes(byte[] bytes, string? fileName = null) => new BytesFileInput(bytes, fileName);

    /// <summary>Creates file input from stream payload.</summary>
    public static FileInput FromStream(Stream stream, string? fileName = null) => new StreamFileInput(stream, fileName);

    public sealed record PathFileInput(string Path) : FileInput;

    public sealed record UrlFileInput(Uri Url) : FileInput;

    public sealed record Base64FileInput(string Base64) : FileInput;

    public sealed record BytesFileInput(byte[] Bytes, string? FileName) : FileInput;

    public sealed record StreamFileInput(Stream Stream, string? FileName) : FileInput;
}
