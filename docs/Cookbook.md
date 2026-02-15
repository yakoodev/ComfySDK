# ComfySdk Cookbook

## 1) Server basic run
```csharp
var client = new ComfyClient(new ComfyClientOptions
{
    BaseUrl = new Uri("http://localhost:8188")
});

var result = await client.RunAsync(new { Prompt = "cat" });
Console.WriteLine(result.PromptId);
```

## 2) Cloud run (ApiPrefix + Bearer)
```csharp
var client = new ComfyClient(new ComfyClientOptions
{
    BaseUrl = new Uri("https://api.comfy.org"),
    ApiPrefix = "/api",
    AuthProvider = AuthProviders.Bearer("<token>")
});
```

## 3) Parallel runs
```csharp
var run1 = client.RunAsync(new { Prompt = "cat" });
var run2 = client.RunAsync(new { Prompt = "dog" });
var run3 = client.RunAsync(new { Prompt = "bird" });
await Task.WhenAll(run1, run2, run3);
```

## 4) FileInput (path + url)
```csharp
var service = new FileUploadService(resolver, uploader);
var pathRef = await service.ResolveAndUploadAsync(FileInput.FromPath("image.png"));
var urlRef = await service.ResolveAndUploadAsync(FileInput.FromUrl(new Uri("https://example.com/image.png")));
```

## 5) Redirect handling for downloads
- Use `ComfyClient.DownloadAsync`; redirects are followed automatically by `ComfyHttpClient.GetWithRedirectsAsync`.
- Pass `promptId` when available for richer diagnostics.

## 6) WS reconnect behavior
- `RunStreamAsync` emits `Disconnected` and attempts reconnect when:
  - `EnableWsReconnect = true`
  - `WsMaxReconnectAttempts > 0`
- If reconnect fails, SDK resolves terminal state via HTTP fallback.

## 7) Output selection tips
- Select outputs by type/name before downloading to avoid unnecessary transfers.
- For persisted outputs, prefer deterministic naming strategy in your own download pipeline.
- If endpoint can return mixed artifacts, guard by `type` (`image`, `video`, `audio`, etc).
