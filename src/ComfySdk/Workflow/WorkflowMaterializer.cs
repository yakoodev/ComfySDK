using System.Text.Json.Nodes;
using System.Text.Json;
using ComfySdk.Exceptions;
using ComfySdk.Files;
using ComfySdk.Settings;

namespace ComfySdk.Workflow;

public sealed record SeedTarget(NodeSelector Selector, string InputName = "seed");

public sealed record SeedApplication(SeedTarget Target, SeedPolicy Policy, long? Value = null);

public sealed record WorkflowMaterializationRequest(
    IReadOnlyList<WorkflowPatch>? Patches = null,
    SeedApplication? Seed = null,
    IReadOnlyList<WorkflowPatch>? Overrides = null);

public sealed record ParameterizedWorkflowMaterializationRequest(
    object Parameters,
    SettingsSpec Settings,
    FileUploadService? FileUploadService = null,
    SeedApplication? Seed = null,
    IReadOnlyList<WorkflowPatch>? Overrides = null);

public sealed record WorkflowMaterializationResult(
    string WorkflowJson,
    long? AppliedSeed,
    string? DiagnosticsWorkflowPath = null);

public sealed class WorkflowMaterializer
{
    private readonly Func<long> _randomSeedFactory;

    public WorkflowMaterializer(Func<long>? randomSeedFactory = null)
    {
        _randomSeedFactory = randomSeedFactory ?? (() => Random.Shared.NextInt64(0, long.MaxValue));
    }

    public WorkflowMaterializationResult Materialize(
        WorkflowTemplate template,
        WorkflowMaterializationRequest? request = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        var working = WorkflowTemplate.Parse(template.RawJson);
        var patches = request?.Patches ?? [];
        var overrides = request?.Overrides ?? [];

        foreach (var patch in patches)
        {
            working.ApplyPatch(patch);
        }

        long? appliedSeed = null;
        if (request?.Seed is not null)
        {
            appliedSeed = request.Seed.Policy.Resolve(request.Seed.Value, _randomSeedFactory);
            var seedPatch = new WorkflowPatch(
                request.Seed.Target.Selector,
                $"inputs.{request.Seed.Target.InputName}",
                JsonValue.Create(appliedSeed.Value));
            working.ApplyPatch(seedPatch);
        }

        foreach (var patch in overrides)
        {
            working.ApplyPatch(patch);
        }

        return new WorkflowMaterializationResult(working.ToJson(), appliedSeed);
    }

    public async ValueTask<WorkflowMaterializationResult> MaterializeAsync(
        WorkflowTemplate template,
        ParameterizedWorkflowMaterializationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Parameters);
        ArgumentNullException.ThrowIfNull(request.Settings);

        SettingsSpecValidator.Validate(request.Settings);

        var working = WorkflowTemplate.Parse(template.RawJson);
        foreach (var parameter in request.Settings.Parameters)
        {
            var patch = await BuildPatchFromParameterAsync(request, parameter, cancellationToken).ConfigureAwait(false);
            if (patch is null)
            {
                continue;
            }

            try
            {
                working.ApplyPatch(patch);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                throw new SpecValidationException(
                    $"Failed to apply parameter '{parameter.Name}' to path '{parameter.Path}': {ex.Message}",
                    ex);
            }
        }

        long? appliedSeed = null;
        if (request.Seed is not null)
        {
            appliedSeed = request.Seed.Policy.Resolve(request.Seed.Value, _randomSeedFactory);
            var seedPatch = new WorkflowPatch(
                request.Seed.Target.Selector,
                $"inputs.{request.Seed.Target.InputName}",
                JsonValue.Create(appliedSeed.Value));
            working.ApplyPatch(seedPatch);
        }

        foreach (var patch in request.Overrides ?? [])
        {
            working.ApplyPatch(patch);
        }

        var materializedJson = working.ToJson();
        var diagnosticsPath = await SaveDiagnosticsAsync(request.Settings.Diagnostics, materializedJson, cancellationToken)
            .ConfigureAwait(false);

        return new WorkflowMaterializationResult(materializedJson, appliedSeed, diagnosticsPath);
    }

    private static async ValueTask<WorkflowPatch?> BuildPatchFromParameterAsync(
        ParameterizedWorkflowMaterializationRequest request,
        SettingsParameterSpec parameter,
        CancellationToken cancellationToken)
    {
        var hasValue = TryReadParameterValue(request.Parameters, parameter.Name, out var parameterValue);
        if (!hasValue || parameterValue is null)
        {
            if (parameter.Default is not null)
            {
                return new WorkflowPatch(parameter.Selector, parameter.Path, parameter.Default.DeepClone());
            }

            if (parameter.Required)
            {
                throw new SpecValidationException($"Required parameter '{parameter.Name}' is missing.");
            }

            return null;
        }

        JsonNode? valueNode;
        if (string.Equals(parameter.Type, "file", StringComparison.OrdinalIgnoreCase))
        {
            if (parameterValue is not FileInput fileInput)
            {
                throw new SpecValidationException(
                    $"Parameter '{parameter.Name}' expects type FileInput, but got '{parameterValue.GetType().Name}'.");
            }

            var conversion = parameter.File?.Conversion ?? "upload";
            if (!string.Equals(conversion, "upload", StringComparison.OrdinalIgnoreCase))
            {
                throw new SpecValidationException(
                    $"Unsupported file conversion '{conversion}' for parameter '{parameter.Name}'.");
            }

            if (request.FileUploadService is null)
            {
                throw new SpecValidationException(
                    $"Parameter '{parameter.Name}' requires file upload, but FileUploadService is not provided.");
            }

            var reference = await request.FileUploadService
                .ResolveAndUploadAsync(fileInput, cancellationToken)
                .ConfigureAwait(false);
            valueNode = JsonValue.Create(reference);
        }
        else
        {
            valueNode = JsonSerializer.SerializeToNode(parameterValue);
        }

        return new WorkflowPatch(parameter.Selector, parameter.Path, valueNode);
    }

    private static bool TryReadParameterValue(object parameters, string name, out object? value)
    {
        var properties = parameters.GetType().GetProperties();
        var candidateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            name,
            ToPascalCase(name)
        };

        foreach (var property in properties)
        {
            if (!property.CanRead)
            {
                continue;
            }

            if (!candidateNames.Contains(property.Name))
            {
                continue;
            }

            value = property.GetValue(parameters);
            return true;
        }

        value = null;
        return false;
    }

    private static async ValueTask<string?> SaveDiagnosticsAsync(
        SettingsDiagnosticsSpec? diagnostics,
        string workflowJson,
        CancellationToken cancellationToken)
    {
        if (diagnostics is null || !diagnostics.SaveMaterializedWorkflow)
        {
            return null;
        }

        var directory = string.IsNullOrWhiteSpace(diagnostics.Directory)
            ? Path.Combine(Path.GetTempPath(), "ComfySdk", "diagnostics")
            : diagnostics.Directory;

        Directory.CreateDirectory(directory);
        var path = Path.Combine(
            directory,
            $"workflow.{DateTime.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, workflowJson, cancellationToken).ConfigureAwait(false);
        return path;
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var parts = name
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return name;
        }

        return string.Concat(parts.Select(static part =>
            char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
