using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private PresenterHotkeys? _presenterHotkeys;
    private void PowerPointUsbPopupOpened(object? sender, EventArgs e) => PowerPointUsbCheck.Focus();
    private void PowerPointUsbPopupKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        PowerPointUsbOptions.IsChecked = false; PowerPointUsbOptions.Focus(); e.Handled = true;
    }

    private void SetupPresenterHotkeys()
    {
        if (_presenterHotkeys is not null) return;
        _presenterHotkeys = new PresenterHotkeys(new WindowInteropHelper(this).Handle, HandlePresenterAdvance);
        ApplyPresenterSettings();
    }

    private void HandlePresenterAdvance(int direction)
    {
        if (!_ready || _closing) return;
        try { AdvancePresentation(direction); }
        catch (Exception error) { SetStatus("No se pudo avanzar con el presentador. " + error.Message, true); }
    }

    private bool ApplyPresenterSettings()
    {
        if (_presenterHotkeys is null) return false;
        if (!_workspace.PresenterEnabled) _presenterHotkeys.Disable();
        else if (!_presenterHotkeys.TryEnable(_workspace.PresenterUsesArrows, out var error))
        {
            _workspace.PresenterEnabled = false;
            UpdatePresenterMenus();
            SetStatus("No se pudo activar el presentador USB. Las teclas pueden estar ocupadas por otra aplicación. " + error, true);
            return false;
        }
        UpdatePresenterMenus();
        return true;
    }

    private void UpdatePresenterMenus()
    {
        if (FindName("PresenterEnabledMenu") is MenuItem enabled) enabled.IsChecked = _presenterHotkeys?.IsEnabled == true;
        if (FindName("PresenterArrowsMenu") is MenuItem arrows) arrows.IsChecked = _workspace.PresenterUsesArrows;
        UpdatePowerPointPanel();
    }

    private void TogglePresenterClick(object sender, RoutedEventArgs e)
    {
        _workspace.PresenterEnabled = sender is MenuItem item ? item.IsChecked : !_workspace.PresenterEnabled;
        var applied = ApplyPresenterSettings();
        SaveWorkspace();
        if (applied) SetStatus(_workspace.PresenterEnabled
            ? "Presentador USB activo: avanza la salida aunque trabajes en otra aplicación."
            : "Presentador USB desactivado: sus teclas vuelven a la aplicación activa.");
    }

    private void TogglePresenterArrowsClick(object sender, RoutedEventArgs e)
    {
        var previousMode = _workspace.PresenterUsesArrows;
        var wasEnabled = _workspace.PresenterEnabled;
        _workspace.PresenterUsesArrows = sender is MenuItem item ? item.IsChecked : !previousMode;
        if (!ApplyPresenterSettings())
        {
            _workspace.PresenterUsesArrows = previousMode;
            _workspace.PresenterEnabled = wasEnabled;
            ApplyPresenterSettings();
            SaveWorkspace();
            return;
        }
        SaveWorkspace();
        SetStatus(_workspace.PresenterUsesArrows
            ? "Presentador configurado para flechas derecha e izquierda."
            : "Presentador configurado para Av Pág y Re Pág.");
    }

    private void DisposePresenterHotkeys()
    {
        _presenterHotkeys?.Dispose();
        _presenterHotkeys = null;
    }
}
