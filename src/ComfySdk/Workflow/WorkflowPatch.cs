using System.Text;
using System.Text.Json.Nodes;

namespace ComfySdk.Workflow;

public sealed record WorkflowPatch(NodeSelector Selector, string Path, JsonNode? Value);

internal static class JsonPathSetter
{
    public static void Set(JsonNode target, string path, JsonNode? value)
    {
        var tokens = Parse(path);
        if (tokens.Count == 0)
        {
            throw new ArgumentException("Patch path cannot be empty.", nameof(path));
        }

        JsonNode? current = target;
        for (var i = 0; i < tokens.Count - 1; i++)
        {
            current = Navigate(current, tokens[i], path);
        }

        SetLeaf(current, tokens[^1], value, path);
    }

    private static JsonNode? Navigate(JsonNode? current, PathToken token, string fullPath)
    {
        if (token.Kind == TokenKind.Property)
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(token.PropertyName!, out var next))
            {
                throw new InvalidOperationException($"Path '{fullPath}' is invalid. Missing property '{token.PropertyName}'.");
            }

            return next;
        }

        if (current is not JsonArray arr || token.Index < 0 || token.Index >= arr.Count)
        {
            throw new InvalidOperationException($"Path '{fullPath}' is invalid. Index [{token.Index}] is out of range.");
        }

        return arr[token.Index];
    }

    private static void SetLeaf(JsonNode? current, PathToken token, JsonNode? value, string fullPath)
    {
        var valueClone = value?.DeepClone();
        if (token.Kind == TokenKind.Property)
        {
            if (current is not JsonObject obj)
            {
                throw new InvalidOperationException($"Path '{fullPath}' is invalid at leaf property '{token.PropertyName}'.");
            }

            obj[token.PropertyName!] = valueClone;
            return;
        }

        if (current is not JsonArray arr || token.Index < 0 || token.Index >= arr.Count)
        {
            throw new InvalidOperationException($"Path '{fullPath}' is invalid. Index [{token.Index}] is out of range.");
        }

        arr[token.Index] = valueClone;
    }

    private static IReadOnlyList<PathToken> Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var tokens = new List<PathToken>();
        var i = 0;
        while (i < path.Length)
        {
            if (path[i] == '.')
            {
                i++;
                continue;
            }

            if (path[i] == '[')
            {
                var closeIndex = path.IndexOf(']', i + 1);
                if (closeIndex <= i + 1)
                {
                    throw new FormatException($"Invalid path '{path}'. Expected '[index]'.");
                }

                var value = path[(i + 1)..closeIndex];
                if (!int.TryParse(value, out var index))
                {
                    throw new FormatException($"Invalid path '{path}'. '{value}' is not an array index.");
                }

                tokens.Add(PathToken.Indexed(index));
                i = closeIndex + 1;
                continue;
            }

            var sb = new StringBuilder();
            while (i < path.Length && path[i] != '.' && path[i] != '[')
            {
                sb.Append(path[i]);
                i++;
            }

            if (sb.Length == 0)
            {
                throw new FormatException($"Invalid path '{path}'.");
            }

            tokens.Add(PathToken.Named(sb.ToString()));
        }

        return tokens;
    }

    private readonly record struct PathToken(TokenKind Kind, string? PropertyName, int Index)
    {
        public static PathToken Named(string name) => new(TokenKind.Property, name, -1);

        public static PathToken Indexed(int index) => new(TokenKind.Index, null, index);
    }

    private enum TokenKind
    {
        Property,
        Index
    }
}
