using ComfySdk;
using ComfySdk.Auth;
using ComfySdk.DependencyInjection;
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

var services = new ServiceCollection();
services.AddComfyClient(cloudOptions);
using var serviceProvider = services.BuildServiceProvider();
var diCloudClient = serviceProvider.GetRequiredService<ComfyClient>();

var directRun = RunScenarioAsync("server-direct", directClient);
var cloudRun = RunScenarioAsync("cloud-di", diCloudClient);
await Task.WhenAll(directRun, cloudRun);

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
