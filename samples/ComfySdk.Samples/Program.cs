using ComfySdk;
using ComfySdk.Options;

var options = new ComfyClientOptions
{
    BaseUrl = new Uri("http://localhost:8188"),
};

var client = new ComfyClient(options);
await foreach (var runEvent in client.RunStreamAsync(new { Prompt = "cat" }))
{
    Console.WriteLine($"[{runEvent.Type}] {runEvent.Message}");
}
