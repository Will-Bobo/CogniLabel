using CogniLabel.Shared.Enums;

namespace CogniLabel.Application.Dtos;

public sealed class AuditResult
{
    public required IReadOnlyList<object> Items { get; init; }
    public required AuditSummary Summary { get; init; }
    public required IReadOnlyList<AuditError> Errors { get; init; }
    public required AuditMeta Meta { get; init; }
}

public sealed class AuditSummary
{
    public required int Total { get; init; }
    public required int Pass { get; init; }
    public required int Fail { get; init; }
}

public sealed class AuditError
{
    public required ErrorType Type { get; init; }
    public required string Message { get; init; }
}

public sealed class AuditMeta
{
    public required DateTimeOffset StartTime { get; init; }
    public required IReadOnlyList<StageResult> Stages { get; init; }
    public required bool Cancelled { get; init; }
}

