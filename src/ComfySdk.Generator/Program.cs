using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ComfySdk.Exceptions;
using ComfySdk.Files;
using ComfySdk.Settings;
using ComfySdk.Workflow;

var argsMap = ParseArgs(args);
if (!argsMap.TryGetValue("workflow", out var workflowPath) ||
    !argsMap.TryGetValue("settings", out var settingsPath) ||
    !argsMap.TryGetValue("out", out var outDir))
{
    Console.Error.WriteLine("Usage: comfysdk-gen --workflow <path> --settings <path> --out <dir>");
    return 1;
}

if (!File.Exists(workflowPath))
{
    Console.Error.WriteLine($"Workflow not found: {workflowPath}");
    return 2;
}

if (!File.Exists(settingsPath))
{
    Console.Error.WriteLine($"Settings not found: {settingsPath}");
    return 3;
}

Directory.CreateDirectory(outDir);
var workflowJson = File.ReadAllText(workflowPath);
SettingsSpec settingsSpec;
try
{
    settingsSpec = SettingsSpecParser.ParseFile(settingsPath);
    ValidateSelectorAndPathBindings(workflowJson, settingsSpec);
}
catch (SpecValidationException ex)
{
    Console.Error.WriteLine($"Spec validation failed: {ex.Message}");
    return 4;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to parse settings/workflow: {ex.Message}");
    return 5;
}

var workflowBaseName = Path.GetFileNameWithoutExtension(workflowPath);
var classBaseName = ToPascalIdentifier(workflowBaseName);
var className = classBaseName + "Workflow";
var outputPath = Path.Combine(outDir, className + ".cs");
var code = BuildCode(className, workflowPath, settingsPath, settingsSpec);

File.WriteAllText(outputPath, code, Encoding.UTF8);
Console.WriteLine($"Generated: {outputPath}");
return 0;

static string BuildCode(
    string className,
    string workflowPath,
    string settingsPath,
    SettingsSpec spec)
{
    var needsFileInput = spec.Parameters.Any(static p =>
        string.Equals(p.Type, "file", StringComparison.OrdinalIgnoreCase));

    var sb = new StringBuilder();
    if (needsFileInput)
    {
        sb.AppendLine("using ComfySdk.Files;");
        sb.AppendLine();
    }

    sb.AppendLine("namespace ComfySdk.Generated;");
    sb.AppendLine();
    sb.AppendLine($"/// <summary>Generated parameters from {Path.GetFileName(workflowPath)} + {Path.GetFileName(settingsPath)}.</summary>");
    sb.AppendLine($"public sealed class {className}");
    sb.AppendLine("{");

    foreach (var parameter in spec.Parameters)
    {
        var propertyName = ToPascalCase(parameter.Name);
        var typeName = MapType(parameter);
        var requiredModifier = parameter.Required && parameter.Default is null ? "required " : string.Empty;

        sb.AppendLine($"    /// <summary>{EscapeForXml(parameter.Description ?? $"Parameter '{parameter.Name}'.")}</summary>");
        sb.AppendLine($"    public {requiredModifier}{typeName} {propertyName} {{ get; init; }}{BuildDefaultInitializer(parameter)}");
        sb.AppendLine();
    }

    sb.AppendLine("}");
    return sb.ToString();
}

static void ValidateSelectorAndPathBindings(string workflowJson, SettingsSpec spec)
{
    foreach (var parameter in spec.Parameters)
    {
        try
        {
            var template = WorkflowTemplate.Parse(workflowJson);
            var value = BuildValidationValue(parameter);
            template.ApplyPatch(new WorkflowPatch(parameter.Selector, parameter.Path, value));
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new SpecValidationException(
                $"Parameter '{parameter.Name}' has invalid selector/path binding: {ex.Message}",
                ex);
        }
    }
}

static JsonNode? BuildValidationValue(SettingsParameterSpec parameter)
{
    if (parameter.Default is not null)
    {
        return parameter.Default.DeepClone();
    }

    return parameter.Type.ToLowerInvariant() switch
    {
        "string" => JsonValue.Create("x"),
        "int" => JsonValue.Create(1),
        "long" => JsonValue.Create(1L),
        "double" => JsonValue.Create(1.0),
        "bool" => JsonValue.Create(true),
        "file" => JsonValue.Create("uploaded://reference"),
        _ => JsonValue.Create("x")
    };
}

static string MapType(SettingsParameterSpec parameter)
{
    var isRequiredWithoutDefault = parameter.Required && parameter.Default is null;
    var nullableSuffix = isRequiredWithoutDefault ? string.Empty : "?";

    return parameter.Type.ToLowerInvariant() switch
    {
        "string" => "string" + nullableSuffix,
        "int" => "int" + nullableSuffix,
        "long" => "long" + nullableSuffix,
        "double" => "double" + nullableSuffix,
        "bool" => "bool" + nullableSuffix,
        "file" => nameof(FileInput) + nullableSuffix,
        _ => throw new SpecValidationException($"Unsupported parameter type '{parameter.Type}'.")
    };
}

static string BuildDefaultInitializer(SettingsParameterSpec parameter)
{
    if (parameter.Default is null)
    {
        return string.Empty;
    }

    var literal = parameter.Type.ToLowerInvariant() switch
    {
        "string" => BuildStringLiteral(parameter.Default),
        "int" => BuildNumberLiteral<int>(parameter.Default),
        "long" => BuildNumberLiteral<long>(parameter.Default) + "L",
        "double" => BuildDoubleLiteral(parameter.Default),
        "bool" => BuildBoolLiteral(parameter.Default),
        _ => string.Empty
    };

    return string.IsNullOrEmpty(literal) ? string.Empty : $" = {literal};";
}

static string BuildStringLiteral(JsonNode defaultNode)
{
    var value = defaultNode.GetValue<string>();
    return "@\"" + value.Replace("\"", "\"\"") + "\"";
}

static string BuildNumberLiteral<T>(JsonNode defaultNode)
    where T : struct
{
    var value = defaultNode.GetValue<T>();
    return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
        ?? throw new SpecValidationException("Failed to render numeric literal.");
}

static string BuildDoubleLiteral(JsonNode defaultNode)
{
    var value = defaultNode.GetValue<double>();
    return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

static string BuildBoolLiteral(JsonNode defaultNode)
{
    var value = defaultNode.GetValue<bool>();
    return value ? "true" : "false";
}

static string ToPascalCase(string name)
{
    var parts = name.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
    {
        return name;
    }

    return string.Concat(parts.Select(static part =>
        char.ToUpperInvariant(part[0]) + part[1..]));
}

static string ToPascalIdentifier(string fileNameWithoutExtension)
{
    if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
    {
        return "Workflow";
    }

    var tokens = Regex.Matches(fileNameWithoutExtension, "[A-Za-z0-9]+")
        .Select(static m => m.Value)
        .Where(static t => !string.IsNullOrWhiteSpace(t))
        .ToArray();

    if (tokens.Length == 0)
    {
        return "Workflow";
    }

    var candidate = string.Concat(tokens.Select(static token =>
        char.ToUpperInvariant(token[0]) + token[1..]));

    if (char.IsDigit(candidate[0]))
    {
        return "Workflow" + candidate;
    }

    return candidate;
}

static string EscapeForXml(string value)
{
    return value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}

static Dictionary<string, string> ParseArgs(string[] argv)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < argv.Length; i++)
    {
        var token = argv[i];
        if (!token.StartsWith("--", StringComparison.Ordinal) || i + 1 >= argv.Length)
        {
            continue;
        }

        map[token[2..]] = argv[++i];
    }

    return map;
}
