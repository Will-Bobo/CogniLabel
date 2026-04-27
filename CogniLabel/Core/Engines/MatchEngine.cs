using CogniLabel.Shared.Enums;

namespace CogniLabel.Core.Engines;

public static class MatchEngine
{
    public static MatchOutcome MatchBySn(
        string? sn,
        IReadOnlyList<Dictionary<string, string>> excelRows,
        string excelSnColumn)
    {
        if (sn is null)
            return MatchOutcome.Skipped();

        var normalizedSn = sn.Trim();
        if (normalizedSn.Length == 0)
            return MatchOutcome.Skipped();

        var matches = excelRows
            .Where(r => r.TryGetValue(excelSnColumn, out var v) && string.Equals(v.Trim(), normalizedSn, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 0)
            return MatchOutcome.Fail(ErrorType.NotFound);

        if (matches.Count > 1)
            return MatchOutcome.Fail(ErrorType.Duplicate);

        return MatchOutcome.Success(matches[0]);
    }
}

public sealed class MatchOutcome
{
    private MatchOutcome(bool isMatched, bool isSkipped, Dictionary<string, string>? row, ErrorType error)
    {
        IsMatched = isMatched;
        IsSkipped = isSkipped;
        Row = row;
        Error = error;
    }

    public bool IsMatched { get; }
    public bool IsSkipped { get; }
    public Dictionary<string, string>? Row { get; }
    public ErrorType Error { get; }

    public static MatchOutcome Success(Dictionary<string, string> row) => new(isMatched: true, isSkipped: false, row: row, error: ErrorType.None);
    public static MatchOutcome Fail(ErrorType error) => new(isMatched: false, isSkipped: false, row: null, error: error);
    public static MatchOutcome Skipped() => new(isMatched: false, isSkipped: true, row: null, error: ErrorType.None);
}

