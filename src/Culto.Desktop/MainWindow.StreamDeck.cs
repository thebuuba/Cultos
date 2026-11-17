using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Culto.Desktop.Services;
using Culto.Remote;

namespace Culto.Desktop;

public partial class MainWindow
{
    private StreamDeckHotkeys? _streamDeckHotkeys;
    private bool _handlingStreamDeck;

    private void SetupStreamDeckHotkeys()
    {
        _streamDeckHotkeys ??= new StreamDeckHotkeys(new WindowInteropHelper(this).Handle, HandleStreamDeckAction);
        ApplyStreamDeckSettings();
    }

    private void ApplyStreamDeckSettings()
    {
        if (_streamDeckHotkeys is null) return;
        if (!_workspace.StreamDeckEnabled) _streamDeckHotkeys.Disable();
        else if (!_streamDeckHotkeys.TryEnable(out var error))
        {
            _workspace.StreamDeckEnabled = false;
            SetStatus("No se pudo activar Stream Deck: una tecla está ocupada. " + error, true);
        }
        StreamDeckEnabledMenu.IsChecked = _streamDeckHotkeys.IsEnabled;
    }

    private void ToggleStreamDeckClick(object sender, RoutedEventArgs e)
    {
        var requested = sender is MenuItem menu ? menu.IsChecked : !_workspace.StreamDeckEnabled;
        _workspace.StreamDeckEnabled = requested;
        ApplyStreamDeckSettings(); SaveWorkspace();
        if (_streamDeckHotkeys?.IsEnabled == true) SetStatus("Stream Deck activo: F13 a F24 controlan Culto Studio en segundo plano.");
        else if (!requested) SetStatus("Stream Deck desactivado.");
    }

    private void HandleStreamDeckAction(string action)
    {
        if (!_ready || _closing) return;
        var previous = _handlingStreamDeck;
        _handlingStreamDeck = true;
        try
        {
            if (action == "project") { ProjectPreparedWithoutActivation(); return; }
            var command = action switch
            {
                "volume-down" => new RemoteCommand("volume", "", Value: VolumeSlider.Value - 5),
                "volume-up" => new RemoteCommand("volume", "", Value: VolumeSlider.Value + 5),
                "scene-youtube" => new RemoteCommand("scene", "", ButtonId: "youtube"),
                "scene-image" => new RemoteCommand("scene", "", ButtonId: "image"),
                _ => new RemoteCommand(action, "")
            };
            var result = ExecuteRemote(command);
            SetStatus(result.Message, !result.Success);
        }
        catch (Exception error) { SetStatus("No se pudo ejecutar Stream Deck. " + error.Message, true); }
        finally { _handlingStreamDeck = previous; }
    }

    private void StreamDeckHelpClick(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        "En la aplicación de Elgato, añade Sistema → Tecla de acceso rápido (Hotkey) y asigna:\n\n" +
        "F13 · Proyectar preparado\nF14 · Anterior\nF15 · Siguiente\nF16 · Logo\nF17 · Negro\n" +
        "F18 · Pausar / continuar\nF19 · Reiniciar\nF20 · Volumen −5\nF21 · Volumen +5\n" +
        "F22 · Volver al contenido\nF23 · Escena YouTube\nF24 · Escena imagen\n\n" +
        "Activa el control en Configuración → Stream Deck. Funciona mientras Culto Studio está abierto, aunque uses otra aplicación. " +
        "La reproducción de YouTube se controla desde Chrome. Las escenas usan el contenido configurado en la PC.",
        "Configurar Stream Deck", MessageBoxButton.OK, MessageBoxImage.Information);

    private void DisposeStreamDeckHotkeys()
    {
        _streamDeckHotkeys?.Dispose(); _streamDeckHotkeys = null;
    }
}
