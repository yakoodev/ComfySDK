# Task D1_1 - Samples and Cookbook

## Done
- Added sample scenarios for:
  - server run
  - cloud run (`ApiPrefix` + `Bearer`)
  - parallel runs (3 concurrent scenarios)
  - `FileInput` flow for `FromPath` and `FromUrl`
- Added cookbook with recipes and anti-footguns:
  - redirect handling
  - WS reconnect behavior
  - output selection strategy
- Updated root `README.md` with sample run commands and cookbook link.

## How To Verify
- Build and run sample:
  - `dotnet build samples/ComfySdk.Samples/ComfySdk.Samples.csproj`
  - `dotnet run --project samples/ComfySdk.Samples/ComfySdk.Samples.csproj`
- Confirm sample output contains:
  - parallel run sections (`server-direct`, `cloud-di`, `cloud-direct`)
  - file input references (`[fileinput] path ref=...`, `[fileinput] url ref=...`)

## Where To Look
- `samples/ComfySdk.Samples/Program.cs`
- `docs/Cookbook.md`
- `README.md`
