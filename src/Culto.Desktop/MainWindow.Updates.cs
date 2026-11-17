using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private static readonly HttpClient UpdateHttp = new() { Timeout = TimeSpan.FromMinutes(15) };
    private readonly AppUpdateClient _updateClient = new(UpdateHttp);
    private readonly CancellationTokenSource _updateCancellation = new();
    private AppRelease? _availableUpdate;
    private bool _checkingUpdates;
    private string? _pendingUpdateInstaller;

    private async Task InitializeUpdatesAsync()
    {
        AutomaticUpdatesMenu.IsChecked = _workspace.CheckForUpdatesOnStartup;
        if (!_workspace.CheckForUpdatesOnStartup) return;
        try { await Task.Delay(4000, _updateCancellation.Token); }
        catch (OperationCanceledException) { return; }
        if (!_closing && _workspace.CheckForUpdatesOnStartup) await CheckUpdatesAsync(false);
    }

    private void ToggleAutomaticUpdatesClick(object sender, RoutedEventArgs e)
    {
        _workspace.CheckForUpdatesOnStartup = AutomaticUpdatesMenu.IsChecked;
        SaveWorkspace();
    }

    private async void CheckUpdatesClick(object sender, RoutedEventArgs e) => await CheckUpdatesAsync(true);

    private async Task CheckUpdatesAsync(bool manual)
    {
        if (_checkingUpdates || _closing) return;
        _checkingUpdates = true;
        CheckUpdatesMenu.IsEnabled = false;
        if (manual) SetStatus("Buscando actualizaciones…");
        try
        {
            var version = typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            _availableUpdate = await _updateClient.CheckAsync(version, _updateCancellation.Token);
            if (_closing) return;
            UpdateAvailableButton.Visibility = _availableUpdate is null ? Visibility.Collapsed : Visibility.Visible;
            if (_availableUpdate is { } update)
            {
                UpdateAvailableButton.Content = $"Versión {update.Version} disponible";
                if (manual) ShowAvailableUpdate();
            }
            else if (manual) MessageBox.Show(this, $"No hay una versión más reciente publicada.\nVersión instalada: {version}", "Actualizaciones", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { if (manual && !_closing) SetStatus("No se pudo comprobar la actualización. Revisa internet y vuelve a intentarlo.", true); }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException)
        { if (manual && !_closing) SetStatus("No se pudo buscar actualizaciones. " + error.Message, true); }
        finally { _checkingUpdates = false; if (!_closing) CheckUpdatesMenu.IsEnabled = true; }
    }

    private void ShowAvailableUpdateClick(object sender, RoutedEventArgs e) => ShowAvailableUpdate();

    private void ShowAvailableUpdate()
    {
        if (_availableUpdate is null || _closing) return;
        var dialog = new AppUpdateDialog(_updateClient, _availableUpdate, Path.Combine(_store.DirectoryPath, "Updates")) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.InstallerPath is null) return;
        var message = _output is null
            ? "Se guardará el culto, se cerrará Culto Studio y se abrirá el instalador.\n\n¿Instalar ahora?"
            : "Hay una salida de proyección abierta. Instalar detendrá la proyección, guardará el culto y cerrará Culto Studio.\n\n¿Detener la proyección e instalar ahora?";
        if (MessageBox.Show(this, message, "Instalar actualización", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        _pendingUpdateInstaller = dialog.InstallerPath;
        Close();
    }

    private void OpenPendingUpdateInstaller()
    {
        if (_pendingUpdateInstaller is not { } installer) return;
        try { using var process = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true }); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        { MessageBox.Show($"No se pudo abrir el instalador. Puedes ejecutarlo desde:\n{installer}\n\n{error.Message}", "Actualización pendiente", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
