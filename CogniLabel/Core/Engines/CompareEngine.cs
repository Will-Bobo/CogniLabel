using CogniLabel.Shared.Enums;

namespace CogniLabel.Core.Engines;

public static class CompareEngine
{
    public static IReadOnlyList<FieldCompareResult> Compare(
        IReadOnlyDictionary<string, string?> imageFields,
        IReadOnlyDictionary<string, string> excelRow,
        IReadOnlyDictionary<string, string> fieldMappings)
    {
        var results = new List<FieldCompareResult>();

        foreach (var (templateField, excelColumn) in fieldMappings)
        {
            imageFields.TryGetValue(templateField, out var imageValue);
            excelRow.TryGetValue(excelColumn, out var excelValue);

            results.Add(CompareOne(templateField, imageValue, excelValue));
        }

        return results;
    }

    public static FieldCompareResult CompareOne(string fieldName, string? imageValue, string? excelValue)
    {
        if (imageValue is null)
            return FieldCompareResult.Fail(fieldName, ErrorType.Unreadable);

        var left = imageValue.Trim();
        var right = (excelValue ?? string.Empty).Trim();

        if (string.Equals(left, right, StringComparison.Ordinal))
            return FieldCompareResult.Pass(fieldName);

        return FieldCompareResult.Fail(fieldName, ErrorType.Mismatch);
    }
}

public sealed class FieldCompareResult
{
    private FieldCompareResult(string fieldName, bool isPass, ErrorType? errorType)
    {
        FieldName = fieldName;
        IsPass = isPass;
        ErrorType = errorType;
    }

    public string FieldName { get; }
    public bool IsPass { get; }
    public ErrorType? ErrorType { get; }

    public static FieldCompareResult Pass(string fieldName) => new(fieldName, isPass: true, errorType: null);
    public static FieldCompareResult Fail(string fieldName, ErrorType errorType) => new(fieldName, isPass: false, errorType: errorType);
}

