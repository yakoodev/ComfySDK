using ComfySdk.Options;

namespace ComfySdk.Files;

public static class FileUploaderFactory
{
    public static IFileUploader Create(ComfyClientOptions options, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        return options.EndpointKind switch
        {
            ComfyEndpointKind.Server => new ServerFileUploader(httpClient, options.BaseUrl, options.RouteMap, options.AuthProvider),
            ComfyEndpointKind.Cloud => new CloudFileUploader(httpClient, options.BaseUrl, options.AuthProvider),
            _ => throw new InvalidOperationException($"Unsupported endpoint kind: {options.EndpointKind}.")
        };
    }
}
