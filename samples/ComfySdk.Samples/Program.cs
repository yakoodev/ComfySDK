using ComfySdk;
using ComfySdk.Auth;
using ComfySdk.DependencyInjection;
using ComfySdk.Files;
using ComfySdk.Models;
using ComfySdk.Options;
using Microsoft.Extensions.DependencyInjection;

var serverOptions = new ComfyClientOptions
{
    BaseUrl = new Uri("http://localhost:8188"),
};

var cloudOptions = new ComfyClientOptions
{
    BaseUrl = new Uri("https://api.comfy.org"),
    ApiPrefix = "/api",
    AuthProvider = AuthProviders.Bearer("demo-cloud-token"),
};

var directClient = new ComfyClient(serverOptions);
var directCloudClient = new ComfyClient(cloudOptions);

var services = new ServiceCollection();
services.AddComfyClient(cloudOptions);
using var serviceProvider = services.BuildServiceProvider();
var diCloudClient = serviceProvider.GetRequiredService<ComfyClient>();

var directRun = RunScenarioAsync("server-direct", directClient);
var cloudRun = RunScenarioAsync("cloud-di", diCloudClient);
var cloudDirectRun = RunScenarioAsync("cloud-direct", directCloudClient);
await Task.WhenAll(directRun, cloudRun, cloudDirectRun);

await RunFileInputScenarioAsync();

static async Task RunScenarioAsync(string name, ComfyClient client)
{
    Console.WriteLine($"-- {name} run stream start");
    await foreach (var runEvent in client.RunStreamAsync(new { Prompt = "cat" }))
    {
        Console.WriteLine($"[{name}] {runEvent.Type}: {runEvent.Message}");
    }

    RunResult result;
    try
    {
        result = await client.RunAsync(new { Prompt = "cat" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{name}] run skipped/failed in local sample: {ex.Message}");
        return;
    }

    Console.WriteLine($"[{name}] PromptId={result.PromptId}, outputs={result.Outputs.Count}");

    var firstImage = result.Outputs.FirstOrDefault(o => o.Type == "image" && o.Url is not null);
    if (firstImage is null)
    {
        Console.WriteLine($"[{name}] no image output to download");
        return;
    }

    try
    {
        var bytes = await client.DownloadAsync(new ViewParams(firstImage.Url!.ToString()), result.PromptId);
        Console.WriteLine($"[{name}] downloaded first image bytes={bytes.Length}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{name}] download skipped/failed in local sample: {ex.Message}");
    }
}

static async Task RunFileInputScenarioAsync()
{
    Console.WriteLine("-- fileinput run start");

    var tempDir = Path.Combine(Path.GetTempPath(), "comfysdk-samples");
    Directory.CreateDirectory(tempDir);

    var localFile = Path.Combine(tempDir, "local-input.txt");
    await File.WriteAllTextAsync(localFile, "local sample payload");

    var uploadService = new FileUploadService(new DemoFileResolver(), new DemoFileUploader(), enableCache: false);
    var fromPath = await uploadService.ResolveAndUploadAsync(FileInput.FromPath(localFile));
    var fromUrl = await uploadService.ResolveAndUploadAsync(FileInput.FromUrl(new Uri("https://example.invalid/input.png")));

    Console.WriteLine($"[fileinput] path ref={fromPath}");
    Console.WriteLine($"[fileinput] url ref={fromUrl}");
}

file sealed class DemoFileResolver : IFileResolver
{
    public async ValueTask<ResolvedFile> ResolveAsync(FileInput input, CancellationToken cancellationToken = default)
    {
        return input switch
        {
            FileInput.PathFileInput path =>
                new ResolvedFile(
                    Path.GetFileName(path.Path),
                    "text/plain",
                    await File.ReadAllBytesAsync(path.Path, cancellationToken)),
            FileInput.UrlFileInput url =>
                new ResolvedFile(
                    Path.GetFileName(url.Url.AbsolutePath),
                    "application/octet-stream",
                    System.Text.Encoding.UTF8.GetBytes($"demo:{url.Url}")),
            _ => throw new InvalidOperationException($"Unsupported sample FileInput: {input.GetType().Name}")
        };
    }
}

file sealed class DemoFileUploader : IFileUploader
{
    public ValueTask<string> UploadAsync(ResolvedFile file, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return new ValueTask<string>($"uploaded://{file.FileName}");
    }
}
