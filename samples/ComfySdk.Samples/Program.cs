using ComfySdk;
using ComfySdk.DependencyInjection;
using ComfySdk.Options;
using Microsoft.Extensions.DependencyInjection;

var defaultOptions = new ComfyClientOptions
{
    BaseUrl = new Uri("http://localhost:8188"),
};

var directClient = new ComfyClient(defaultOptions);

var services = new ServiceCollection();
services.AddComfyClient(defaultOptions);
using var serviceProvider = services.BuildServiceProvider();
var diClient = serviceProvider.GetRequiredService<ComfyClient>();

var directRun = RunScenarioAsync("direct", directClient);
var diRun = RunScenarioAsync("di", diClient);
await Task.WhenAll(directRun, diRun);

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
