namespace CogniLabel.Application.Dtos;

public sealed class AuditRequest
{
    public required string ExcelPath { get; init; }
    public string? ImageFolderPath { get; init; }
    public string? TemplatePath { get; init; }
    public required Dictionary<string, string> FieldMappings { get; init; }
}

