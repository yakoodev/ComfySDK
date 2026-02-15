# ComfySdk

Repository scaffold for .NET 10 ComfyUI SDK based on docs in `docs/`.

## Projects
- `src/ComfySdk` - runtime SDK and public API skeleton.
- `src/ComfySdk.Generator` - CLI generator (`comfysdk-gen`) with settings/workflow validation.
- `samples/ComfySdk.Samples` - sample app bootstrap.
- `tests/ComfySdk.Tests` - runtime test scaffold.
- `tests/ComfySdk.Generator.Tests` - generator test scaffold.
- `tests/ComfySdk.SmokeTests` - smoke test scaffold.

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

## Generator usage
```powershell
dotnet run --project src/ComfySdk.Generator -- --workflow workflow.my.json --settings settings.my.json --out Generated
```

Sample input files for C-tasks are available in:
- `samples/ComfySdk.Samples/workflow.sample.json`
- `samples/ComfySdk.Samples/settings.sample.json`
