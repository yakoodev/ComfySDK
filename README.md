# ComfySdk

Repository scaffold for .NET 10 ComfyUI SDK based on docs in `docs/`.

## Projects
- `src/ComfySdk` - runtime SDK and public API skeleton.
- `src/ComfySdk.Generator` - CLI generator (`comfysdk-gen`) with settings/workflow validation.
- `samples/ComfySdk.Samples` - sample app.
- `tests/ComfySdk.Tests` - runtime unit test executable.
- `tests/ComfySdk.Generator.Tests` - generator test scaffold.
- `tests/ComfySdk.SmokeTests` - smoke tests for materialization pipeline.

## Build
Use per-project build commands:

```powershell
dotnet build src/ComfySdk/ComfySdk.csproj
dotnet build src/ComfySdk.Generator/ComfySdk.Generator.csproj
dotnet build samples/ComfySdk.Samples/ComfySdk.Samples.csproj
dotnet build tests/ComfySdk.Tests/ComfySdk.Tests.csproj
dotnet build tests/ComfySdk.Generator.Tests/ComfySdk.Generator.Tests.csproj
dotnet build tests/ComfySdk.SmokeTests/ComfySdk.SmokeTests.csproj
```

## Run Samples
```powershell
dotnet run --project samples/ComfySdk.Samples/ComfySdk.Samples.csproj
```

Sample scenarios include:
- server run (`BaseUrl=http://localhost:8188`)
- cloud run (`ApiPrefix=/api` + bearer auth)
- parallel runs (server + cloud DI + cloud direct)
- `FileInput` upload flow (`FromPath` + `FromUrl`)

## Cookbook
See `docs/Cookbook.md` for recipes and anti-footguns:
- redirect handling
- WS reconnect behavior
- output selection and download strategy

## Generator usage
```powershell
dotnet run --project src/ComfySdk.Generator -- --workflow workflow.my.json --settings settings.my.json --out Generated
```

## Tests
```powershell
dotnet run --project tests/ComfySdk.Tests/ComfySdk.Tests.csproj
dotnet run --project tests/ComfySdk.SmokeTests/ComfySdk.SmokeTests.csproj
```

## NuGet Packaging
```powershell
dotnet pack src/ComfySdk/ComfySdk.csproj -c Release
dotnet pack src/ComfySdk.Generator/ComfySdk.Generator.csproj -c Release
```

Sample input files for C-tasks are available in:
- `samples/ComfySdk.Samples/workflow.sample.json`
- `samples/ComfySdk.Samples/settings.sample.json`
