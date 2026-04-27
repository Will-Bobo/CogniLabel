using CogniLabel.Shared.Enums;

namespace CogniLabel.Core.Engines;

public static class ErrorClassifier
{
    public static RowEvaluation Evaluate(
        bool isImageUnreadable,
        MatchOutcome match,
        IReadOnlyList<FieldCompareResult> fieldResults,
        IReadOnlySet<string> duplicateSns,
        string? sn)
    {
        if (isImageUnreadable)
            return RowEvaluation.Fail(ErrorType.Unreadable, fieldResults);

        if (!match.IsSkipped && match.Error == ErrorType.NotFound)
            return RowEvaluation.Fail(ErrorType.NotFound, fieldResults);

        var normalizedSn = sn?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSn) && duplicateSns.Contains(normalizedSn))
            return RowEvaluation.Fail(ErrorType.Duplicate, fieldResults);

        var hasFieldUnreadable = fieldResults.Any(r => r.ErrorType == ErrorType.Unreadable);
        if (hasFieldUnreadable)
            return RowEvaluation.Fail(ErrorType.Unreadable, fieldResults);

        var hasMismatch = fieldResults.Any(r => r.ErrorType == ErrorType.Mismatch);
        if (hasMismatch)
            return RowEvaluation.Fail(ErrorType.Mismatch, fieldResults);

        return RowEvaluation.Pass(fieldResults);
    }
}

public sealed class RowEvaluation
{
    private RowEvaluation(bool isPass, ErrorType error, IReadOnlyList<FieldCompareResult> fields)
    {
        IsPass = isPass;
        Error = error;
        FieldResults = fields;
    }

    public bool IsPass { get; }
    public ErrorType Error { get; }
    public IReadOnlyList<FieldCompareResult> FieldResults { get; }

    public static RowEvaluation Pass(IReadOnlyList<FieldCompareResult> fields) => new(isPass: true, error: ErrorType.None, fields: fields);
    public static RowEvaluation Fail(ErrorType error, IReadOnlyList<FieldCompareResult> fields) => new(isPass: false, error: error, fields: fields);
}

