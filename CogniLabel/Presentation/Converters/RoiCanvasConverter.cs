using System.Globalization;
using System.Windows.Data;

namespace CogniLabel.Presentation.Converters;

/// <summary>
/// values[0] = roi component (double), values[1] = canvas size (double). Returns roi*canvasSize.
/// </summary>
public sealed class RoiCanvasConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return 0d;

        if (values[0] is not double roi)
            return 0d;
        if (values[1] is not double size)
            return 0d;

        if (double.IsNaN(roi) || double.IsInfinity(roi) || double.IsNaN(size) || double.IsInfinity(size))
            return 0d;

        return roi * size;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

