namespace CogniLabel.Application.Pipeline;

public enum AuditStage
{
    ExcelLoading,
    ExcelValidating,
    TemplateLoading,
    ImageProcessing,
    Matching,
    Comparing,
    Deduplicating,
    Summary,
}

