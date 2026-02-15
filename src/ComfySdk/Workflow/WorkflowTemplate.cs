using System.Text.Json.Nodes;

namespace ComfySdk.Workflow;

public sealed class WorkflowTemplate
{
    private readonly string _rawJson;
    private readonly JsonObject _root;
    private readonly Dictionary<string, JsonObject> _nodesById;

    private WorkflowTemplate(string rawJson, JsonObject root, Dictionary<string, JsonObject> nodesById)
    {
        _rawJson = rawJson;
        _root = root;
        _nodesById = nodesById;
    }

    public static WorkflowTemplate Parse(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException("Workflow JSON root must be an object.");
        var nodes = BuildNodeIndex(root);
        return new WorkflowTemplate(json, root, nodes);
    }

    public IReadOnlyDictionary<string, JsonObject> Nodes => _nodesById;

    public string RawJson => _rawJson;

    public void ApplyPatch(WorkflowPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var matches = FindMatches(patch.Selector);
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Patch selector must match exactly one node, but matched {matches.Count}.");
        }

        JsonPathSetter.Set(matches[0], patch.Path, patch.Value);
    }

    public string ToJson() => _root.ToJsonString();

    private static Dictionary<string, JsonObject> BuildNodeIndex(JsonObject root)
    {
        var index = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var (nodeId, nodeValue) in root)
        {
            if (nodeValue is JsonObject nodeObject &&
                nodeObject["class_type"] is JsonValue classType &&
                classType.TryGetValue<string>(out _))
            {
                index[nodeId] = nodeObject;
            }
        }

        return index;
    }

    private List<JsonObject> FindMatches(NodeSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        IEnumerable<KeyValuePair<string, JsonObject>> candidates = _nodesById;

        if (!string.IsNullOrWhiteSpace(selector.NodeId))
        {
            if (_nodesById.TryGetValue(selector.NodeId, out var byId))
            {
                candidates = new[] { KeyValuePair.Create(selector.NodeId, byId) };
            }
            else
            {
                candidates = [];
            }
        }

        if (!string.IsNullOrWhiteSpace(selector.ClassType))
        {
            candidates = candidates.Where(kv =>
            {
                var classType = kv.Value["class_type"]?.GetValue<string>();
                return string.Equals(classType, selector.ClassType, StringComparison.Ordinal);
            });
        }

        if (!string.IsNullOrWhiteSpace(selector.WhereInputExists))
        {
            candidates = candidates.Where(kv =>
                kv.Value["inputs"] is JsonObject inputs &&
                inputs.ContainsKey(selector.WhereInputExists));
        }

        if (selector.WhereInputValue is not null)
        {
            var filter = selector.WhereInputValue;
            candidates = candidates.Where(kv =>
            {
                if (kv.Value["inputs"] is not JsonObject inputs)
                {
                    return false;
                }

                if (!inputs.TryGetPropertyValue(filter.Name, out var inputValue))
                {
                    return false;
                }

                return JsonNode.DeepEquals(inputValue, filter.Value);
            });
        }

        return candidates.Select(static kv => kv.Value).ToList();
    }
}
