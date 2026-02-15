using System.Text.Json.Nodes;

namespace ComfySdk.Workflow;

public sealed record NodeSelector
{
    public string? NodeId { get; init; }

    public string? ClassType { get; init; }

    public string? WhereInputExists { get; init; }

    public InputValueFilter? WhereInputValue { get; init; }
}

public sealed record InputValueFilter(string Name, JsonNode? Value);
