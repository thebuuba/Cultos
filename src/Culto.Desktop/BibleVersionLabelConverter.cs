using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace Culto.Desktop;

public sealed class BibleVersionLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value as string ?? "";
        var abbreviation = Regex.Match(name, @"\(([^()]+)\)\s*$");
        if (abbreviation.Success) return abbreviation.Groups[1].Value;
        return name switch
        {
            "Biblica® Open Nueva Biblia Viva 2008" => "NBV",
            "Valera 1602 Purificada" => "RV1602P",
            _ => name
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
