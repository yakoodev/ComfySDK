using ComfySdk.Exceptions;
using ComfySdk.Settings;

namespace ComfySdk.Tests;

public static class SettingsSpecParserTests
{
    public static void UnsupportedSchemaVersion_ShouldThrow()
    {
        const string settingsJson = """
{
  "settingsSchemaVersion": 2,
  "parameters": []
}
""";

        var thrown = false;
        try
        {
            _ = SettingsSpecParser.Parse(settingsJson);
        }
        catch (SpecValidationException ex)
        {
            thrown = ex.Message.Contains("settingsSchemaVersion", StringComparison.OrdinalIgnoreCase);
        }

        Ensure(thrown, "Expected schema version validation error.");
    }

    public static void ValidSpec_ShouldParse()
    {
        const string settingsJson = """
{
  "settingsSchemaVersion": 1,
  "parameters": [
    {
      "name": "seed",
      "type": "long",
      "selector": { "class_type": "KSampler" },
      "path": "inputs.seed",
      "required": true
    }
  ],
  "diagnostics": {
    "saveMaterializedWorkflow": true
  }
}
""";

        var spec = SettingsSpecParser.Parse(settingsJson);
        Ensure(spec.SettingsSchemaVersion == 1, "Expected version 1.");
        Ensure(spec.Parameters.Count == 1, "Expected one parameter.");
        Ensure(spec.Diagnostics?.SaveMaterializedWorkflow == true, "Expected diagnostics flag enabled.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
