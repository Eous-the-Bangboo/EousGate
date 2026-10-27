using System.Globalization;
using System.Windows.Data;

namespace EousGate;

public sealed class HalfWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double width && double.IsFinite(width) ? Math.Max(0, (width - 2) / 2) : 0d;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
