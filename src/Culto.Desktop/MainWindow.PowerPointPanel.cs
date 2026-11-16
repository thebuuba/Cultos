using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop.Services;
using Microsoft.Win32;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly ObservableCollection<Cue> _powerPointFiles = [];
    private bool _updatingPowerPointPanel;
    private int _powerPointSlideIndex;
    private long _lastPowerPointPositionPoll;
    private static readonly Lazy<bool> PowerPointAvailable = new(() => PowerPointPresentation.IsAvailable);

    private void PowerPointPanelVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (PowerPointPanel.IsVisible) RefreshPowerPointFiles();
    }

    private void PollPowerPointPosition()
    {
        if (_powerPoint?.IsRunning != true || Environment.TickCount64 - _lastPowerPointPositionPoll < 700) return;
        _lastPowerPointPositionPoll = Environment.TickCount64;
        try { UpdatePowerPointPosition(); }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or System.Runtime.InteropServices.InvalidComObjectException)
        { /* Office can be briefly busy during an automatic slide change. Retry on the next poll. */ }
    }

    private void RefreshPowerPointFiles()
    {
        if (!_ready || !PowerPointPanel.IsVisible) return;
        var selected = (PowerPointFiles.SelectedItem as Cue)?.FilePath;
        _updatingPowerPointPanel = true;
        try
        {
            PowerPointFiles.ItemsSource = _powerPointFiles;
            _powerPointFiles.Clear();
            foreach (var cue in _workspace.Library.Concat(_plan).Where(c => c.Kind == CueKind.PowerPoint)
                .DistinctBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase).OrderBy(c => c.Title)) _powerPointFiles.Add(cue);
            PowerPointFiles.SelectedItem = _powerPointFiles.FirstOrDefault(c => c.FilePath == selected);
        }
        finally { _updatingPowerPointPanel = false; }
        UpdatePowerPointPanel();
    }

    private async void ImportPowerPointClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Agregar presentaciones de PowerPoint", Multiselect = true,
            Filter = "PowerPoint|*.pptx;*.ppt;*.ppsx;*.pps" };
        if (dialog.ShowDialog(this) != true) return;
        await ImportMediaAsync(dialog.FileNames);
        PowerPointFiles.SelectedItem = _powerPointFiles.FirstOrDefault(c => string.Equals(c.FilePath, dialog.FileNames[0], StringComparison.OrdinalIgnoreCase));
    }

    private void PowerPointSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _updatingPowerPointPanel) return;
        if (PowerPointFiles.SelectedItem is Cue cue) PrepareCue(cue);
        UpdatePowerPointPanel();
    }
    private void ProjectPowerPointClick(object sender, RoutedEventArgs e)
    {
        if (PowerPointFiles.SelectedItem is not Cue cue) { SetStatus("Selecciona una presentación de PowerPoint."); return; }
        PrepareCue(cue); ProjectPrepared(); UpdatePowerPointPanel();
    }
    private void PowerPointDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || ContentRowAt(PowerPointFiles, e.OriginalSource)?.DataContext is not Cue) return;
        e.Handled = true; ProjectPowerPointClick(sender, new RoutedEventArgs());
    }
    private void PowerPointKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.IsRepeat) return;
        e.Handled = true; ProjectPowerPointClick(sender, new RoutedEventArgs());
    }
    private void AddPowerPointToPlanClick(object sender, RoutedEventArgs e)
    { if (PowerPointFiles.SelectedItem is Cue cue) AddToPlan(cue); }

    private void PowerPointPreviousClick(object sender, RoutedEventArgs e) => NavigatePowerPointCard(-1);
    private void PowerPointNextClick(object sender, RoutedEventArgs e) => NavigatePowerPointCard(1);
    private void NavigatePowerPointCard(int direction)
    {
        if (_session.Live?.Kind != CueKind.PowerPoint || _powerPoint?.IsRunning != true) return;
        AdvancePresentation(direction);
    }
    private void PowerPointPauseClick(object sender, RoutedEventArgs e)
    { if (_session.Live?.Kind == CueKind.PowerPoint) { TogglePause(); UpdatePowerPointPanel(); } }
    private void PowerPointRestartClick(object sender, RoutedEventArgs e)
    { if (_session.Live?.Kind == CueKind.PowerPoint) { RestartMedia(); UpdatePowerPointPosition(); } }
    private void PowerPointUsbKeysChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _updatingPowerPointPanel || PowerPointUsbKeys.SelectedIndex < 0) return;
        if ((PowerPointUsbKeys.SelectedIndex == 1) != _workspace.PresenterUsesArrows) TogglePresenterArrowsClick(sender, new RoutedEventArgs());
    }

    private void UpdatePowerPointPanel()
    {
        if (!_ready) return;
        var running = _session.Live?.Kind == CueKind.PowerPoint && _powerPoint?.IsRunning == true;
        var controllable = running && !_session.IsBlackout && !_session.IsTextHidden && !_pendingCuePlayback.IsPending;
        PowerPointPlaybackPanel.Visibility = Visibility.Visible;
        PowerPointPlaybackPanel.IsEnabled = controllable;
        PowerPointModeratorButton.IsEnabled = running;
        PowerPointProjectButton.IsEnabled = PowerPointFiles.SelectedItem is Cue && PowerPointAvailable.Value;
        PowerPointAddToPlanButton.IsEnabled = PowerPointFiles.SelectedItem is Cue;
        PowerPointEmpty.Visibility = _powerPointFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PowerPointPositionText.Text = running ? $"Diapositiva {_powerPointSlideIndex} de {_powerPoint!.SlideCount}" : "Sin presentación en pantalla";
        PowerPointLiveText.Text = running ? "En pantalla: " + _session.Live!.Title
            : _pendingPowerPoint is not null ? "Preparando presentación…"
            : PowerPointAvailable.Value ? "Selecciona un archivo y pulsa Proyectar." : "Instala Microsoft PowerPoint para presentar estos archivos.";
        PowerPointPauseButton.Content = _isPaused ? "Continuar" : "Pausar";
        _updatingPowerPointPanel = true;
        try
        {
            PowerPointUsbCheck.IsChecked = _presenterHotkeys?.IsEnabled == true;
            PowerPointUsbKeys.SelectedIndex = _workspace.PresenterUsesArrows ? 1 : 0;
            PowerPointUsbStatus.Text = _presenterHotkeys?.IsEnabled == true
                ? "USB activo · Anterior / Siguiente, incluso desde otra ventana."
                : "Actívalo para usar los botones del presentador USB.";
        }
        finally { _updatingPowerPointPanel = false; }
        UpdatePowerPointModerator();
    }
}
