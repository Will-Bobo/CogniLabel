namespace CogniLabel.Application.SingleImage;

public sealed class ImageProcessResult
{
    public required string ImagePath { get; init; }
    public required string ImageName { get; init; }
    public required Dictionary<string, string?> Fields { get; init; }
    public Dictionary<string, IReadOnlyList<string>>? RawValues { get; init; }
    public required bool IsUnreadable { get; init; }
}

