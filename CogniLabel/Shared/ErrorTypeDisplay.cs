using CogniLabel.Shared.Enums;

namespace CogniLabel.Shared;

public static class ErrorTypeDisplay
{
    public static string GetErrorDisplay(ErrorType type)
    {
        return type switch
        {
            ErrorType.None => string.Empty,
            ErrorType.NotFound => "NOT_FOUND",
            ErrorType.Mismatch => "MISMATCH",
            ErrorType.Duplicate => "DUPLICATE",
            ErrorType.Unreadable => "UNREADABLE",
            _ => type.ToString(),
        };
    }
}

