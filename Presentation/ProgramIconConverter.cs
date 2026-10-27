using System.Globalization;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EousGate;

public sealed class ProgramIconConverter : IValueConverter
{
    private readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string { Length: > 0 } path) return null;
        if (_cache.TryGetValue(path, out var cached)) return cached;
        ImageSource? source = null;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is not null) { var bitmap = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32)); bitmap.Freeze(); source = bitmap; }
        }
        catch (Exception ex) when (ex is ArgumentException or System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
        if (_cache.Count >= 128) _cache.Clear();
        _cache[path] = source; return source;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
