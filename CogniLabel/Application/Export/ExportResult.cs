namespace CogniLabel.Application.Export;

public sealed class ExportResult
{
    public required bool IsSuccess { get; init; }
    public string? OutputPath { get; init; }
    public required string Message { get; init; }
}

