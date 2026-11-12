using System.Globalization;
using System.Windows.Data;

namespace Culto.Desktop;

public sealed class MediaPathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var path = value as string ?? "";
        return parameter as string == "Extension" ? Path.GetExtension(path).TrimStart('.').ToUpperInvariant()
            : Path.GetFileName(path);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
