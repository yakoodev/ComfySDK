using System.Text;

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
var name = Path.GetFileNameWithoutExtension(workflowPath)
    .Replace("workflow.", string.Empty, StringComparison.OrdinalIgnoreCase)
    .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
    .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase);
if (string.IsNullOrWhiteSpace(name))
{
    name = "Workflow";
}

var className = char.ToUpperInvariant(name[0]) + name[1..] + "Params";
var outputPath = Path.Combine(outDir, className + ".cs");
var code = $$"""
namespace ComfySdk.Generated;

/// <summary>Generated parameters scaffold from {{Path.GetFileName(workflowPath)}} + {{Path.GetFileName(settingsPath)}}.</summary>
public sealed class {{className}}
{
}
""";

File.WriteAllText(outputPath, code, Encoding.UTF8);
Console.WriteLine($"Generated: {outputPath}");
return 0;

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
