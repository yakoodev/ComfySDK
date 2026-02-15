using System.Text.Json.Nodes;
using ComfySdk.Exceptions;
using ComfySdk.Files;
using ComfySdk.Settings;
using ComfySdk.Workflow;

namespace ComfySdk.Tests;

public static class WorkflowMaterializerAsyncTests
{
    public static async Task FileParameter_IsUploaded_AndDiagnosticsSaved()
    {
        const string workflowJson = """
{
  "20": {
    "class_type": "LoadImage",
    "inputs": { "image": "input.png" }
  }
}
""";

        var tempDir = Path.Combine(Path.GetTempPath(), $"comfysdk-diag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var settings = new SettingsSpec
            {
                SettingsSchemaVersion = 1,
                Parameters =
                [
                    new SettingsParameterSpec
                    {
                        Name = "input_image",
                        Type = "file",
                        Selector = new NodeSelector { NodeId = "20" },
                        Path = "inputs.image",
                        Required = true,
                        File = new SettingsFileParameterSpec { Conversion = "upload" }
                    }
                ],
                Diagnostics = new SettingsDiagnosticsSpec
                {
                    SaveMaterializedWorkflow = true,
                    Directory = tempDir
                }
            };

            var resolver = new StubResolver();
            var uploader = new StubUploader();
            var uploadService = new FileUploadService(resolver, uploader, enableCache: false);
            var materializer = new WorkflowMaterializer();
            var request = new ParameterizedWorkflowMaterializationRequest(
                new MaterializeParams { InputImage = FileInput.FromBytes([1, 2, 3], "a.png") },
                settings,
                uploadService);

            var result = await materializer.MaterializeAsync(
                WorkflowTemplate.Parse(workflowJson),
                request);

            var root = JsonNode.Parse(result.WorkflowJson)!.AsObject();
            var imageValue = root["20"]!["inputs"]!["image"]!.GetValue<string>();
            Ensure(imageValue == "uploaded://a.png", $"Expected uploaded file ref, got '{imageValue}'.");
            Ensure(!string.IsNullOrWhiteSpace(result.DiagnosticsWorkflowPath), "Expected diagnostics path.");
            Ensure(File.Exists(result.DiagnosticsWorkflowPath), "Expected diagnostics file to exist.");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    public static async Task RequiredParameter_WhenMissing_ShouldThrow()
    {
        const string workflowJson = """
{
  "20": {
    "class_type": "LoadImage",
    "inputs": { "image": "input.png" }
  }
}
""";

        var settings = new SettingsSpec
        {
            SettingsSchemaVersion = 1,
            Parameters =
            [
                new SettingsParameterSpec
                {
                    Name = "input_image",
                    Type = "file",
                    Selector = new NodeSelector { NodeId = "20" },
                    Path = "inputs.image",
                    Required = true,
                    File = new SettingsFileParameterSpec { Conversion = "upload" }
                }
            ]
        };

        var materializer = new WorkflowMaterializer();
        var thrown = false;
        try
        {
            _ = await materializer.MaterializeAsync(
                WorkflowTemplate.Parse(workflowJson),
                new ParameterizedWorkflowMaterializationRequest(new object(), settings, null));
        }
        catch (SpecValidationException ex)
        {
            thrown = ex.Message.Contains("Required parameter", StringComparison.OrdinalIgnoreCase);
        }

        Ensure(thrown, "Expected validation exception when required parameter is missing.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record MaterializeParams
    {
        public FileInput? InputImage { get; init; }
    }

    private sealed class StubResolver : IFileResolver
    {
        public ValueTask<ResolvedFile> ResolveAsync(FileInput input, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return new ValueTask<ResolvedFile>(new ResolvedFile("a.png", "image/png", [1, 2, 3]));
        }
    }

    private sealed class StubUploader : IFileUploader
    {
        public ValueTask<string> UploadAsync(ResolvedFile file, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return new ValueTask<string>($"uploaded://{file.FileName}");
        }
    }
}
