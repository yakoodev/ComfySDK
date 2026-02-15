using System.Text.RegularExpressions;
using ComfySdk.Models;

namespace ComfySdk.Outputs;

public static class OutputSelector
{
    public static IReadOnlyList<OutputArtifact> Select(
        IEnumerable<OutputArtifact> artifacts,
        OutputSelectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(settings);

        var filtered = artifacts
            .Where(a => MatchesType(a, settings.Types))
            .Where(a => MatchesName(a, settings.NamePatterns))
            .ToList();

        return settings.Mode switch
        {
            OutputSelectionMode.All => filtered,
            OutputSelectionMode.First => filtered.Take(1).ToList(),
            OutputSelectionMode.ByName => filtered,
            _ => throw new InvalidOperationException($"Unsupported selection mode: {settings.Mode}.")
        };
    }

    private static bool MatchesType(OutputArtifact artifact, IReadOnlyList<string> types)
    {
        if (types.Count == 0 || types.Any(t => string.Equals(t, "any", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return types.Any(t => string.Equals(t, artifact.Type, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesName(OutputArtifact artifact, IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return true;
        }

        return patterns.Any(pattern => GlobMatch(artifact.Name, pattern));
    }

    private static bool GlobMatch(string input, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
