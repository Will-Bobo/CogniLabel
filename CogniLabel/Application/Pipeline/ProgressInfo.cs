namespace CogniLabel.Application.Pipeline;

public sealed class ProgressInfo
{
    public required int Current { get; init; }
    public required int Total { get; init; }
    public required AuditStage Stage { get; init; }
    public required string Message { get; init; }
}

