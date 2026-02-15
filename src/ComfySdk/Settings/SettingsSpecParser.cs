using System.Text.Json.Nodes;
using ComfySdk.Exceptions;
using ComfySdk.Workflow;

namespace ComfySdk.Settings;

public static class SettingsSpecParser
{
    public static SettingsSpec Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new SpecValidationException("Settings JSON root must be an object.");

        var spec = new SettingsSpec
        {
            SettingsSchemaVersion = ReadInt(root, "settingsSchemaVersion", required: true),
            Parameters = ReadParameters(root),
            Outputs = ReadOutputs(root),
            Diagnostics = ReadDiagnostics(root)
        };

        SettingsSpecValidator.Validate(spec);
        return spec;
    }

    public static SettingsSpec ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new SpecValidationException($"Settings file not found: {path}");
        }

        var json = File.ReadAllText(path);
        return Parse(json);
    }

    private static IReadOnlyList<SettingsParameterSpec> ReadParameters(JsonObject root)
    {
        if (root["parameters"] is null)
        {
            return [];
        }

        if (root["parameters"] is not JsonArray parametersArray)
        {
            throw new SpecValidationException("Property 'parameters' must be an array.");
        }

        var items = new List<SettingsParameterSpec>(parametersArray.Count);
        for (var i = 0; i < parametersArray.Count; i++)
        {
            if (parametersArray[i] is not JsonObject param)
            {
                throw new SpecValidationException($"Parameter at index {i} must be an object.");
            }

            var selectorObject = ReadObject(param, "selector", required: true)!;
            var whereInputValueObject = ReadObject(selectorObject, "whereInputValue", required: false)
                ?? ReadObject(selectorObject, "where_input_value", required: false);

            var selector = new NodeSelector
            {
                NodeId = ReadString(selectorObject, "nodeId", required: false)
                    ?? ReadString(selectorObject, "node_id", required: false),
                ClassType = ReadString(selectorObject, "classType", required: false)
                    ?? ReadString(selectorObject, "class_type", required: false),
                WhereInputExists = ReadString(selectorObject, "whereInputExists", required: false)
                    ?? ReadString(selectorObject, "where_input_exists", required: false),
                WhereInputValue = whereInputValueObject is null
                    ? null
                    : new InputValueFilter(
                        ReadString(whereInputValueObject, "name", required: true)!,
                        CloneNode(whereInputValueObject["value"]))
            };

            var fileObject = ReadObject(param, "file", required: false);
            var fileSpec = fileObject is null
                ? null
                : new SettingsFileParameterSpec
                {
                    Conversion = ReadString(fileObject, "conversion", required: false) ?? "upload"
                };

            items.Add(new SettingsParameterSpec
            {
                Name = ReadString(param, "name", required: true)!,
                Type = ReadString(param, "type", required: true)!,
                Selector = selector,
                Path = ReadString(param, "path", required: true)!,
                Required = ReadBool(param, "required", required: false) ?? false,
                Default = CloneNode(param["default"]),
                Description = ReadString(param, "description", required: false),
                File = fileSpec
            });
        }

        return items;
    }

    private static SettingsOutputsSpec? ReadOutputs(JsonObject root)
    {
        var outputs = ReadObject(root, "outputs", required: false);
        if (outputs is null)
        {
            return null;
        }

        return new SettingsOutputsSpec
        {
            Mode = ReadString(outputs, "mode", required: false) ?? "all",
            Types = ReadStringArray(outputs, "types"),
            NamePatterns = ReadStringArray(outputs, "namePatterns"),
            Download = ReadString(outputs, "download", required: false) ?? "none",
            SaveDir = ReadString(outputs, "saveDir", required: false),
            FileName = ReadString(outputs, "fileName", required: false) ?? "guid"
        };
    }

    private static SettingsDiagnosticsSpec? ReadDiagnostics(JsonObject root)
    {
        var diagnostics = ReadObject(root, "diagnostics", required: false);
        if (diagnostics is null)
        {
            return null;
        }

        return new SettingsDiagnosticsSpec
        {
            SaveMaterializedWorkflow = ReadBool(diagnostics, "saveMaterializedWorkflow", required: false) ?? false,
            Directory = ReadString(diagnostics, "directory", required: false)
        };
    }

    private static JsonObject? ReadObject(JsonObject parent, string propertyName, bool required)
    {
        if (!parent.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            if (required)
            {
                throw new SpecValidationException($"Required property '{propertyName}' is missing.");
            }

            return null;
        }

        if (node is not JsonObject obj)
        {
            throw new SpecValidationException($"Property '{propertyName}' must be an object.");
        }

        return obj;
    }

    private static string? ReadString(JsonObject parent, string propertyName, bool required)
    {
        if (!parent.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            if (required)
            {
                throw new SpecValidationException($"Required property '{propertyName}' is missing.");
            }

            return null;
        }

        if (node is not JsonValue value || !value.TryGetValue<string>(out var str))
        {
            throw new SpecValidationException($"Property '{propertyName}' must be a string.");
        }

        if (required && string.IsNullOrWhiteSpace(str))
        {
            throw new SpecValidationException($"Property '{propertyName}' cannot be empty.");
        }

        return str;
    }

    private static int ReadInt(JsonObject parent, string propertyName, bool required)
    {
        if (!parent.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            if (required)
            {
                throw new SpecValidationException($"Required property '{propertyName}' is missing.");
            }

            return 0;
        }

        if (node is not JsonValue value || !value.TryGetValue<int>(out var number))
        {
            throw new SpecValidationException($"Property '{propertyName}' must be an integer.");
        }

        return number;
    }

    private static bool? ReadBool(JsonObject parent, string propertyName, bool required)
    {
        if (!parent.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            if (required)
            {
                throw new SpecValidationException($"Required property '{propertyName}' is missing.");
            }

            return null;
        }

        if (node is not JsonValue value || !value.TryGetValue<bool>(out var flag))
        {
            throw new SpecValidationException($"Property '{propertyName}' must be a boolean.");
        }

        return flag;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject parent, string propertyName)
    {
        if (!parent.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return [];
        }

        if (node is not JsonArray array)
        {
            throw new SpecValidationException($"Property '{propertyName}' must be an array.");
        }

        var result = new List<string>(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
            {
                throw new SpecValidationException($"Property '{propertyName}[{i}]' must be a non-empty string.");
            }

            result.Add(text);
        }

        return result;
    }

    private static JsonNode? CloneNode(JsonNode? node) => node?.DeepClone();
}

public static class SettingsSpecValidator
{
    private static readonly HashSet<string> SupportedParameterTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string",
        "int",
        "long",
        "double",
        "bool",
        "file"
    };

    public static void Validate(SettingsSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.SettingsSchemaVersion != SettingsSchema.Version1)
        {
            throw new SpecValidationException(
                $"Unsupported settingsSchemaVersion '{spec.SettingsSchemaVersion}'. Supported version is {SettingsSchema.Version1}.");
        }

        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < spec.Parameters.Count; i++)
        {
            var parameter = spec.Parameters[i];
            if (!parameterNames.Add(parameter.Name))
            {
                throw new SpecValidationException($"Duplicate parameter name '{parameter.Name}'.");
            }

            if (!SupportedParameterTypes.Contains(parameter.Type))
            {
                throw new SpecValidationException(
                    $"Unsupported parameter type '{parameter.Type}' for parameter '{parameter.Name}'.");
            }

            if (string.IsNullOrWhiteSpace(parameter.Path))
            {
                throw new SpecValidationException($"Parameter '{parameter.Name}' has empty path.");
            }

            if (string.IsNullOrWhiteSpace(parameter.Selector.NodeId) &&
                string.IsNullOrWhiteSpace(parameter.Selector.ClassType))
            {
                throw new SpecValidationException(
                    $"Parameter '{parameter.Name}' selector must specify at least 'nodeId' or 'class_type'.");
            }

            if (string.Equals(parameter.Type, "file", StringComparison.OrdinalIgnoreCase))
            {
                var conversion = parameter.File?.Conversion ?? "upload";
                if (!string.Equals(conversion, "upload", StringComparison.OrdinalIgnoreCase))
                {
                    throw new SpecValidationException(
                        $"Parameter '{parameter.Name}' uses unsupported file conversion '{conversion}'. Only 'upload' is supported in v1.");
                }
            }
        }
    }
}
