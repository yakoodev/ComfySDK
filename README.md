# ComfySdk

English | [Русский](README.ru.md)

ComfySdk is a .NET SDK for ComfyUI with:
- HTTP client (`submit`, `history`, `download`)
- workflow materialization from `workflow.json` + `settings.*.json`
- code generation for strongly-typed workflow request classes
- sample playground for fast local testing

## Project Layout
- `src/ComfySdk` - runtime SDK
- `src/ComfySdk.Generator` - class generator CLI
- `samples/ComfySdk.Playground` - real end-to-end local playground
- `samples/ComfySdk.Samples` - additional API samples
- `tests/*` - unit and smoke tests

## Generator Naming Rule
Generated class name is:
- `{FileNameWithoutExtension}Workflow`
- converted to C# PascalCase
- non alphanumeric symbols removed

Examples:
- `default.json` -> `DefaultWorkflow` (`DefaultWorkflow.cs`)
- `my-awesome_flow.v2.json` -> `MyAwesomeFlowV2Workflow` (`MyAwesomeFlowV2Workflow.cs`)

## Quick Start (Smallest Practical Flow)

### 1) Put files here
- `samples/ComfySdk.Playground/Specs/Default/default.json`
- `samples/ComfySdk.Playground/Specs/Default/settings.default.json`

### 2) Run playground
```powershell
$env:COMFY_BASE_URL="http://127.0.0.1:8188"
$env:COMFY_SUBMIT="true"
$env:COMFY_PROMPT="china woman 2"
dotnet run -c Release --project samples/ComfySdk.Playground/ComfySdk.Playground.csproj
```

### 3) Minimal code usage (inside your app)
```csharp
var request = new DefaultWorkflow { PositivePrompt = "china woman 2" };
var materialized = await connector.MaterializeAsync(request);
var result = await connector.SubmitAndWaitHistoryAsync(materialized.WorkflowJson, TimeSpan.FromSeconds(90));
```

## Detailed Flow

### Input Files
`default.json`:
- your ComfyUI workflow JSON
- node ids and input paths must match `settings.default.json` selectors/paths

`settings.default.json`:
- schema (`settingsSchemaVersion`)
- parameters (name/type/selector/path/default/required)
- optional diagnostics

### Build-Time Generation
`samples/ComfySdk.Playground/ComfySdk.Playground.csproj` runs generator before build:
- workflow: `Specs/Default/default.json`
- settings: `Specs/Default/settings.default.json`
- output: `Models/`

So build generates `Models/DefaultWorkflow.cs` automatically.

### Runtime Execution
In playground:
1. load workflow + settings
2. create generated request object
3. materialize workflow JSON with patched values
4. submit to ComfyUI
5. poll history until outputs appear

Notes:
- SDK handles `history_v2` -> `history` fallback
- supports object-shaped Comfy history outputs
- if Comfy reports `completed` slightly before outputs are visible, playground applies short grace polling

## Environment Variables (Playground)
- `COMFY_BASE_URL` (default: `http://localhost:8188`)
- `COMFY_SUBMIT` (`true/1/yes`, default: `true`)
- `COMFY_WAIT_SECONDS` (default: `90`)
- `COMFY_PROMPT` (default prompt text)
- `COMFY_SEED` (optional; auto-generated when not set)

## Build & Test
```powershell
dotnet build src/ComfySdk/ComfySdk.csproj
dotnet build src/ComfySdk.Generator/ComfySdk.Generator.csproj
dotnet build samples/ComfySdk.Playground/ComfySdk.Playground.csproj
dotnet run --project tests/ComfySdk.Tests/ComfySdk.Tests.csproj
dotnet run --project tests/ComfySdk.SmokeTests/ComfySdk.SmokeTests.csproj
```
