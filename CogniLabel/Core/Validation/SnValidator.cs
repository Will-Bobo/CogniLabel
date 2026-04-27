namespace CogniLabel.Core.Validation;

public static class SnValidator
{
    public static SnValidationResult Validate(IEnumerable<string?> sns)
    {
        var trimmed = sns.Select(s => s?.Trim()).ToList();
        var hasEmpty = trimmed.Any(s => string.IsNullOrWhiteSpace(s));

        var duplicates = trimmed
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(s => s!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return new SnValidationResult(hasEmpty, duplicates);
    }
}

public sealed class SnValidationResult
{
    public SnValidationResult(bool hasEmpty, IReadOnlyList<string> duplicates)
    {
        HasEmpty = hasEmpty;
        Duplicates = duplicates;
    }

    public bool HasEmpty { get; }
    public IReadOnlyList<string> Duplicates { get; }
}

