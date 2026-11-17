using System.Threading;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Culto.Desktop.Services;
using Culto.Remote;

namespace Culto.Desktop;

public partial class MainWindow
{
    private StreamDeckPluginBridge? _streamDeckPlugin;
    private readonly SemaphoreSlim _streamDeckPluginSettings = new(1, 1);

    private async Task ApplyStreamDeckPluginSettingsAsync()
    {
        await _streamDeckPluginSettings.WaitAsync();
        try
        {
            if (_closing || !_workspace.StreamDeckPluginEnabled)
            {
                if (_streamDeckPlugin is { } existing)
                {
                    _streamDeckPlugin = null;
                    await existing.DisposeAsync();
                }
            }
            else if (_streamDeckPlugin is null)
            {
                var bridge = new StreamDeckPluginBridge(_store.DirectoryPath, GetRemoteState, HandleStreamDeckPluginCommandAsync);
                try
                {
                    await bridge.StartAsync();
                    _streamDeckPlugin = bridge;
                }
                catch
                {
                    await bridge.DisposeAsync();
                    throw;
                }
            }
            StreamDeckPluginEnabledMenu.IsChecked = _streamDeckPlugin is not null;
        }
        catch (Exception error)
        {
            _workspace.StreamDeckPluginEnabled = false;
            StreamDeckPluginEnabledMenu.IsChecked = false;
            if (!_closing) SetStatus("No se pudo activar el plugin de Stream Deck. " + error.Message, true);
        }
        finally { _streamDeckPluginSettings.Release(); }
    }

    private async void ToggleStreamDeckPluginClick(object sender, RoutedEventArgs e)
    {
        var requested = sender is MenuItem menu ? menu.IsChecked : !_workspace.StreamDeckPluginEnabled;
        _workspace.StreamDeckPluginEnabled = requested;
        await ApplyStreamDeckPluginSettingsAsync();
        if (_closing) return;
        SaveWorkspace();
        if (_streamDeckPlugin is not null) SetStatus("Integración nativa de Stream Deck activa. Añade las acciones Culto Studio en Elgato.");
        else if (!requested && !_workspace.StreamDeckPluginEnabled)
            SetStatus("Plugin de Stream Deck desactivado.");
    }

    private async Task DisposeStreamDeckPluginAsync()
    {
        await _streamDeckPluginSettings.WaitAsync();
        try
        {
            if (_streamDeckPlugin is { } bridge)
            {
                _streamDeckPlugin = null;
                await bridge.DisposeAsync();
            }
        }
        finally { _streamDeckPluginSettings.Release(); }
    }

    private async Task<CommandResult> HandleStreamDeckPluginCommandAsync(RemoteCommand command)
        => await Dispatcher.InvokeAsync(() => ExecuteStreamDeckPluginCommand(command));

    private CommandResult ExecuteStreamDeckPluginCommand(RemoteCommand command)
    {
        if (!_ready || _closing) return new(false, "La consola no está disponible.");
        var previous = _handlingStreamDeck;
        var pending = _youTubeLinkProjection;
        _handlingStreamDeck = true;
        try
        {
            var result = ExecuteRemote(command);
            // Navigation and Play continue asynchronously after the native key is accepted.
            if (_youTubeLinkProjection is not null && !ReferenceEquals(pending, _youTubeLinkProjection)
                && command.Action is "project" or "cue" or "scene" or "next" or "previous")
                return new(true, "YouTube aceptado; cargando en Programa.");
            return result;
        }
        finally { _handlingStreamDeck = previous; }
    }

    private void InstallStreamDeckPluginClick(object sender, RoutedEventArgs e)
    {
        var installer = Path.Combine(AppContext.BaseDirectory, "StreamDeck", "com.cultostudio.control.streamDeckPlugin");
        if (!File.Exists(installer))
        {
            SetStatus("No se encuentra el instalador del plugin de Stream Deck. Instala la versión completa de Culto Studio o abre el archivo com.cultostudio.control.streamDeckPlugin entregado con la aplicación.", true);
            return;
        }
        try
        {
            using var process = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true });
            SetStatus("Confirma la instalación en Elgato y añade las acciones Culto Studio a tus botones.");
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        { SetStatus("No se pudo abrir el instalador. Instala la aplicación de Elgato Stream Deck y vuelve a intentarlo. " + error.Message, true); }
    }

    private void StreamDeckPluginHelpClick(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        "Instala el plugin Culto Studio en la aplicación de Elgato y activa Integración nativa aquí.\n\n" +
        "Añade las acciones Culto Studio a tu Stream Deck: se conectan automáticamente a esta consola y muestran su estado. " +
        "Puedes elegir escenas y elementos del orden desde la configuración del botón.\n\n" +
        "Las teclas F13–F24 son una alternativa independiente para perfiles antiguos.",
        "Plugin de Stream Deck", MessageBoxButton.OK, MessageBoxImage.Information);
}
