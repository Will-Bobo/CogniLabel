using CogniLabel.Application.Pipeline;
using CogniLabel.Shared;
using System.Globalization;
using System.Windows.Data;

namespace CogniLabel.Presentation.Converters;

public sealed class EnumToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AuditStage stage)
            return string.Empty;

        return stage switch
        {
            AuditStage.ExcelLoading => Strings.UI.StageExcelLoading,
            AuditStage.ExcelValidating => Strings.UI.StageExcelValidating,
            AuditStage.TemplateLoading => Strings.UI.StageTemplateLoading,
            AuditStage.ImageProcessing => Strings.UI.StageImageProcessing,
            AuditStage.Matching => Strings.UI.StageMatching,
            AuditStage.Comparing => Strings.UI.StageComparing,
            AuditStage.Deduplicating => Strings.UI.StageDeduplicating,
            AuditStage.Summary => Strings.UI.StageSummary,
            _ => stage.ToString(),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

