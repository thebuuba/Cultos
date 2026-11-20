using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop.Services;
using Microsoft.Win32;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly ObservableCollection<Cue> _powerPointFiles = [];
    private Cue? _currentPowerPointFile;
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
        var query = PowerPointSearch.Text.Trim();
        var all = _workspace.Library.Where(c => c.Kind == CueKind.PowerPoint)
            .Concat(_currentPowerPointFile is { } current ? new[] { current } : Array.Empty<Cue>())
            .DistinctBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase).OrderBy(c => c.Title).ToArray();
        _updatingPowerPointPanel = true;
        try
        {
            PowerPointFiles.ItemsSource = _powerPointFiles;
            _powerPointFiles.Clear();
            foreach (var cue in all.Where(c => query.Length == 0 || c.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (c.FilePath?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                || PowerPointFileMetadata.Get(c).Title.Contains(query, StringComparison.OrdinalIgnoreCase))) _powerPointFiles.Add(cue);
            PowerPointFiles.SelectedItem = _powerPointFiles.FirstOrDefault(c => SamePowerPoint(c, _currentPowerPointFile));
            PowerPointFileCountText.Text = $"{_powerPointFiles.Count} de {all.Length} archivos · arrastra un .pptx";
        }
        finally { _updatingPowerPointPanel = false; }
        UpdatePowerPointPanel();
    }

    private async void ImportPowerPointClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Agregar presentaciones de PowerPoint", Multiselect = true,
            Filter = "PowerPoint|*.pptx;*.ppt;*.ppsx;*.pps" };
        if (dialog.ShowDialog(this) != true) return;
        await AddPowerPointFilesAsync(dialog.FileNames);
    }

    private void PowerPointSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _updatingPowerPointPanel) return;
        if (PowerPointFiles.SelectedItem is Cue cue) PrepareCue(cue);
        UpdatePowerPointPanel();
    }
    private void ProjectPowerPointClick(object sender, RoutedEventArgs e)
    {
        if (SelectedPowerPointIsLive) StopPowerPointFromPanel(); else StartSelectedPowerPoint();
    }
    private void PowerPointDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || ContentRowAt(PowerPointFiles, e.OriginalSource)?.DataContext is not Cue) return;
        e.Handled = true; StartSelectedPowerPoint();
    }
    private void PowerPointKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.IsRepeat) return;
        e.Handled = true; StartSelectedPowerPoint();
    }
    private void AddPowerPointToPlanClick(object sender, RoutedEventArgs e)
    { if (_currentPowerPointFile is { } cue) AddToPlan(cue); }

    private void PowerPointPreviousClick(object sender, RoutedEventArgs e) => NavigatePowerPointCard(-1);
    private void PowerPointNextClick(object sender, RoutedEventArgs e) => NavigatePowerPointCard(1);
    private void NavigatePowerPointCard(int direction)
    {
        if (SelectedPowerPointIsLive) { AdvancePresentation(direction); return; }
        if (_powerPointSlidePreviews.Count == 0) return;
        PowerPointSlides.SelectedIndex = Math.Clamp(_powerPointCursor - 1 + direction, 0, _powerPointSlidePreviews.Count - 1);
    }
    private void PowerPointPauseClick(object sender, RoutedEventArgs e)
    { if (_session.Live?.Kind == CueKind.PowerPoint) { TogglePause(); UpdatePowerPointPanel(); } }
    private void PowerPointRestartClick(object sender, RoutedEventArgs e)
    { if (SelectedPowerPointIsLive) { RestartMedia(); UpdatePowerPointPosition(); } else if (_powerPointSlidePreviews.Count > 0) PowerPointSlides.SelectedIndex = 0; }
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
        PowerPointPlaybackPanel.IsEnabled = true;
        PowerPointPreviousButton.IsEnabled = SelectedPowerPointIsLive ? controllable : _powerPointCursor > 1;
        PowerPointNextButton.IsEnabled = SelectedPowerPointIsLive ? controllable : _powerPointCursor < _powerPointSlidePreviews.Count;
        PowerPointRestartButton.IsEnabled = SelectedPowerPointIsLive ? controllable : _powerPointSlidePreviews.Count > 0;
        PowerPointPauseButton.IsEnabled = running;
        PowerPointBlackoutButton.IsEnabled = running;
        PowerPointBlackoutButton.Foreground = (Brush)FindResource(_session.IsBlackout ? "AccentTextBrush" : "TextBrush");
        PowerPointModeratorButton.IsEnabled = running;
        PowerPointProjectButton.IsEnabled = _currentPowerPointFile is not null && PowerPointAvailable.Value;
        PowerPointProjectButton.Content = SelectedPowerPointIsLive ? "Detener" : "Proyectar";
        PowerPointAddToPlanButton.IsEnabled = _currentPowerPointFile is not null;
        PowerPointEmpty.Visibility = _powerPointFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PowerPointPositionText.Text = SelectedPowerPointIsLive ? $"{_powerPointSlideIndex} de {_powerPoint!.SlideCount}"
            : _powerPointSlidePreviews.Count > 0 ? $"{_powerPointCursor} de {_powerPointSlidePreviews.Count}" : "—";
        PowerPointSlides.IsEnabled = !SelectedPowerPointIsLive || controllable;
        if (_powerPointSlidePreviews.Count > 0)
        {
            var selected = SelectedPowerPointIsLive ? _powerPointSlideIndex : _powerPointCursor;
            _updatingPowerPointSlides = true;
            try { PowerPointSlides.SelectedIndex = Math.Clamp(selected - 1, 0, _powerPointSlidePreviews.Count - 1); }
            finally { _updatingPowerPointSlides = false; }
            if (_powerPointSlidePreviews.All(s => s.Image is not null))
                PowerPointSlideHint.Text = SelectedPowerPointIsLive ? "Clic para mostrar en pantalla" : "Clic para elegir inicio";
        }
        PowerPointLiveText.Text = running ? "En pantalla: " + _session.Live!.Title
            : _pendingPowerPoint is not null ? "Preparando presentación…"
            : _powerPoint?.IsRunning == true ? "Disponible en Escenas: " + _powerPointCue?.Title
            : PowerPointAvailable.Value ? "Ninguna presentación en pantalla" : "Instala Microsoft PowerPoint para presentar estos archivos.";
        PowerPointPauseButton.Content = _isPaused ? "Continuar" : "Pausar";
        _updatingPowerPointPanel = true;
        try
        {
            PowerPointUsbCheck.IsChecked = _presenterHotkeys?.IsEnabled == true;
            PowerPointUsbKeys.SelectedIndex = _workspace.PresenterUsesArrows ? 1 : 0;
            PowerPointUsbKeys.IsEnabled = _presenterHotkeys?.IsEnabled == true;
            PowerPointUsbSummary.Text = "Presentador USB · " + (_presenterHotkeys?.IsEnabled == true
                ? (_workspace.PresenterUsesArrows ? "Flechas ← / →" : "Av Pág / Re Pág") : "Apagado");
            PowerPointUsbIndicator.Fill = _presenterHotkeys?.IsEnabled == true ? new SolidColorBrush(Color.FromRgb(132, 195, 24)) : Brushes.Gray;
            PowerPointUsbStatus.Text = _presenterHotkeys?.IsEnabled == true
                ? "Escuchando el control remoto" : "Desactivado";
        }
        finally { _updatingPowerPointPanel = false; }
        UpdatePowerPointModerator();
    }
}
