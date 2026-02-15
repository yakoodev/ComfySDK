using ComfySdk;
using ComfySdk.Auth;
using ComfySdk.DependencyInjection;
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

    var result = await client.RunAsync(new { Prompt = "cat" });
    Console.WriteLine($"[{name}] PromptId={result.PromptId}, outputs={result.Outputs.Count}");
}
