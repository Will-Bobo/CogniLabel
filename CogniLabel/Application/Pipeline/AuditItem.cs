using CogniLabel.Application.SingleImage;
using CogniLabel.Shared.Enums;

namespace CogniLabel.Application.Pipeline;

public sealed class AuditItem
{
    public required ImageProcessResult Image { get; init; }
    public required bool IsPass { get; init; }
    public required ErrorType ErrorType { get; init; }
    public required IReadOnlyList<FieldIssue> FieldIssues { get; init; }
    public IReadOnlyDictionary<string, string>? ExcelValues { get; init; }
}

public sealed class FieldIssue
{
    public required string FieldName { get; init; }
    public required ErrorType ErrorType { get; init; }
}

