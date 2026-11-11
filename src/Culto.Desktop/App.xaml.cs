using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Culto.Core;

namespace Culto.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("es-DO");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es");
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CultoStudio", "errors.log");
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                File.AppendAllText(log, $"{DateTimeOffset.Now:O} {args.Exception}\n");
            }
            catch (IOException) { }
            MessageBox.Show("No se pudo completar la acción.\n\n" + args.Exception.Message, "Culto Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        base.OnStartup(e);
    }
}

public sealed class CueKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is CueKind kind ? kind switch
    {
        CueKind.Bible => "BIBLIA", CueKind.Video => "VIDEO / HIMNO", CueKind.Image => "IMAGEN",
        CueKind.Audio => "AUDIO", _ => "TEXTO"
    } : "CONTENIDO";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
