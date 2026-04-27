namespace CogniLabel.Core.Engines;

public static class DuplicateEngine
{
    public static IReadOnlySet<string> FindDuplicates(IEnumerable<string?> sns)
    {
        var list = sns
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();

        return list
            .GroupBy(s => s, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
    }
}

