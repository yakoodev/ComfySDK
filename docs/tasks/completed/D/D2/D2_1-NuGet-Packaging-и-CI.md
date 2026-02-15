# Task D2_1 - NuGet Packaging and CI

## Done
- Configured package metadata for runtime SDK and generator tool:
  - package id, tags, version prefix, license/repository metadata
  - package readme files included in `.nupkg`
- Kept generator publishing as dotnet tool (`PackAsTool=true`, `ToolCommandName=comfysdk-gen`).
- Added CI workflow:
  - restore + release build
  - runtime unit test executable run
  - smoke test executable run
  - pack runtime and generator packages
  - upload `.nupkg` artifacts
- Replaced smoke placeholder with real materialization smoke test (no real Comfy required).

## How To Verify
- Run local test executables:
  - `dotnet run --project tests/ComfySdk.Tests/ComfySdk.Tests.csproj`
  - `dotnet run --project tests/ComfySdk.SmokeTests/ComfySdk.SmokeTests.csproj`
- Pack both packages:
  - `dotnet pack src/ComfySdk/ComfySdk.csproj -c Release`
  - `dotnet pack src/ComfySdk.Generator/ComfySdk.Generator.csproj -c Release`
- Check CI workflow file:
  - `.github/workflows/ci.yml`

## Where To Look
- `.github/workflows/ci.yml`
- `Directory.Build.props`
- `src/ComfySdk/ComfySdk.csproj`
- `src/ComfySdk.Generator/ComfySdk.Generator.csproj`
- `tests/ComfySdk.SmokeTests/Program.cs`
