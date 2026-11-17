using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;

namespace Culto.Desktop.Dialogs;

public sealed class AppUpdateDialog : Window
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Button _action;
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly AppUpdateClient _client;
    private readonly AppRelease _release;
    private readonly string _directory;
    public string? InstallerPath { get; private set; }

    public AppUpdateDialog(AppUpdateClient client, AppRelease release, string directory)
    {
        _client = client; _release = release; _directory = directory;
        var panel = DialogUi.Panel(this, "Actualización de Culto Studio",
            $"Versión {release.Version} disponible · {release.Size / 1_000_000d:F0} MB.\nPuedes descargarla y decidir cuándo cerrar el programa para instalarla. Tus cultos y ajustes se conservan.");
        if (!string.IsNullOrWhiteSpace(release.Notes))
            panel.Children.Add(new TextBlock { Text = release.Notes, TextWrapping = TextWrapping.Wrap, MaxHeight = 180, Margin = new Thickness(0, 0, 0, 12) });
        _status = new TextBlock { Text = "La instalación comenzará solo cuando la aceptes.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        panel.Children.Add(_status);
        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 12, Visibility = Visibility.Collapsed };
        System.Windows.Automation.AutomationProperties.SetName(_progress, "Progreso de descarga");
        panel.Children.Add(_progress);
        _action = DialogUi.Button("Descargar actualización", () => _ = DownloadOrInstallAsync(), true);
        _action.Name = "DownloadUpdateButton";
        panel.Children.Add(_action);
        var later = DialogUi.Button("Ahora no", Close); later.IsCancel = true;
        panel.Children.Add(later);
        Closed += (_, _) => _cancellation.Cancel();
    }

    private async Task DownloadOrInstallAsync()
    {
        if (InstallerPath is not null) { DialogResult = true; return; }
        _action.IsEnabled = false;
        _progress.Visibility = Visibility.Visible;
        try
        {
            _status.Text = "Descargando… puedes cancelar y seguir trabajando.";
            InstallerPath = await _client.DownloadAsync(_release, _directory, new Progress<double>(value => {
                if (_cancellation.IsCancellationRequested) return;
                _progress.Value = value;
                _status.Text = value < 100 ? $"Descargando… {value:F0} %" : "Verificando la descarga…";
            }), _cancellation.Token);
            if (_cancellation.IsCancellationRequested) return;
            _status.Text = "Descarga verificada. Guarda tu trabajo y cierra la salida de proyección antes de instalar.";
            _action.Content = "Instalar y cerrar Culto Studio";
        }
        catch (OperationCanceledException) { if (!_cancellation.IsCancellationRequested) _status.Text = "La descarga tardó demasiado. Puedes volver a intentarlo."; }
        catch (Exception error) when (error is System.Net.Http.HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        { _status.Text = "No se pudo descargar la actualización. " + error.Message; }
        finally { if (!_cancellation.IsCancellationRequested) _action.IsEnabled = true; }
    }
}
