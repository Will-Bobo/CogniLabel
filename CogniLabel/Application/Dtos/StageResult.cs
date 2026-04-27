using CogniLabel.Shared.Enums;

namespace CogniLabel.Application.Dtos;

public sealed class StageResult
{
    public required bool IsSuccess { get; init; }
    public required bool ShouldStop { get; init; }
    public required string Message { get; init; }
    public ErrorType? ErrorType { get; init; }
    public object? Payload { get; init; }

    public static StageResult Success(string message, object? payload = null)
        => new() { IsSuccess = true, ShouldStop = false, Message = message, Payload = payload };

    public static StageResult Stop(ErrorType errorType, string message, object? payload = null)
        => new()
        {
            IsSuccess = false,
            ShouldStop = true,
            Message = message,
            ErrorType = errorType,
            Payload = payload,
        };
}

