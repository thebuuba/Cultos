using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Culto.Core;

namespace Culto.Desktop;

public partial class App : Application
{
    private FileStream? _workspaceSession;
    static App()
    {
        // Draw the selection behind glyphs so opaque highlights honor SelectionTextBrush.
        AppContext.SetSwitch("Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering", false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.CapturedApplicationAudio.ShutdownShared();
        Services.BrowserSourceRuntime.Shutdown();
        _workspaceSession?.Dispose();
        base.OnExit(e);
    }
    private void ButtonHoverChanged(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button &&
            button.Template.FindName("ButtonHighlight", button) is UIElement highlight)
            UiMotion.Fade(highlight, button.IsMouseOver ? .07 : 0);
    }

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
        try { _workspaceSession = Services.WorkspaceSession.Open(new Services.WorkspaceStore().DirectoryPath); }
        catch (IOException error) when ((error.HResult & 0xFFFF) is 32 or 33)
        {
            MessageBox.Show("Culto Studio ya está abierto con estos datos. Usa la ventana abierta o ciérrala antes de abrir otra versión para conservar tus ajustes.",
                "Culto Studio", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("No se pudo abrir la carpeta de datos.\n\n" + error.Message,
                "Culto Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(); return;
        }
        base.OnStartup(e);
        // Native startup owns one console; an explicit host can supply its own.
        if (MainWindow is null) new MainWindow().Show();
    }
}

public sealed class PillRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new CornerRadius(value is double height && double.IsFinite(height) ? Math.Max(0, height / 2) : 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class CueKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is CueKind kind ? kind switch
    {
        CueKind.Bible => "BIBLIA", CueKind.Video => "VIDEO / HIMNO", CueKind.Image => "IMAGEN",
        CueKind.Audio => "AUDIO", CueKind.PowerPoint => "POWERPOINT", CueKind.WindowCapture => "VENTANA", CueKind.BrowserSource => "NAVEGADOR", _ => "TEXTO"
    } : "CONTENIDO";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
