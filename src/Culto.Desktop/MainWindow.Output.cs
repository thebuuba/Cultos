using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void ProjectClick(object sender, RoutedEventArgs e) => ProjectPrepared();
    private void ProjectPrepared()
    {
        var cue = _session.Prepared;
        if (cue is null) { SetStatus("Selecciona primero un contenido para preparar."); return; }
        if (!CheckMedia(cue)) return;
        if (!EnsureOutput()) return;
        _liveReading = _preparedReading;
        _session.GoLive();
        if (_session.Live?.Kind == CueKind.Video) _output!.TransitionTo(_session.Live);
        else _output!.ShowCue(_session.Live);
        if (_session.Live?.Kind is CueKind.Video or CueKind.Audio) _output.SetPaused(false);
        _isPaused = false;
        UpdateSessionLabels();
        SetStatus($"En pantalla: {cue.Title}" + (_outputIsTest ? " · Ventana de ensayo." : "."));
        Activate();
    }
    private bool CheckMedia(Cue cue)
    {
        if (cue.Kind is not (CueKind.Image or CueKind.Video or CueKind.Audio)) return true;
        try
        {
            if (string.IsNullOrWhiteSpace(cue.FilePath) || !File.Exists(cue.FilePath))
                throw new FileNotFoundException("No se encuentra el archivo.");
            using var stream = File.Open(cue.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (cue.Kind == CueKind.Image)
                _ = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or FileFormatException)
        {
            SetStatus("No se puede abrir «" + cue.Title + "». La proyección actual se conserva. " + error.Message, true);
            return false;
        }
    }
    private bool EnsureOutput()
    {
        if (_outputWindow is not null) return true;
        try
        {
            var displays = DisplayService.GetDisplays();
            var display = DisplayService.SelectOutput(displays, _workspace.DisplayDevice, DisplayService.GetWindowDevice(this));
            if (display is null)
            {
                OutputStatusText.Text = "Sin pantalla externa";
                SetStatus("No hay una pantalla externa disponible. Conecta el HDMI y selecciona Extender en Windows.", true);
                return false;
            }
            OpenOutput(display, test: false);
            return true;
        }
        catch (Exception ex) { SetStatus("No se pudo abrir la proyección: " + ex.Message, true); return false; }
    }
    private void OpenOutput(DisplayInfo display, bool test)
    {
        CloseOutput();
        _output = new ProjectionSurface();
        _output.Volume = (int)VolumeSlider.Value;
        _output.Loop = LoopCheck.IsChecked == true;
        _output.PlaybackError += message => Dispatcher.BeginInvoke(() => { _isPaused = true; SetStatus(message, true); });
        _outputWindow = new ProjectionWindow(_output);
        _outputDisplay = display; _outputIsTest = test;
        var window = _outputWindow;
        window.Closed += (_, _) =>
        {
            if (!ReferenceEquals(window, _outputWindow)) return;
            _output?.SetPaused(true); _output?.Dispose();
            _output = null; _outputWindow = null; _outputDisplay = null;
            LiveMonitor.SetSource(null);
            _isPaused = true; UpdateSessionLabels();
            SetStatus("Salida cerrada.");
        };
        DisplayService.PlaceWindow(window, display, fullscreen: !test);
        window.Show();
        LiveMonitor.SetSource(window);
        // Restore the last output, never the independently prepared cue. Retain suppression and pause.
        _output.ShowCue(_session.Live, blackout: true);
        _output.ShowCue(_session.Live, _session.IsBlackout, _session.IsTextHidden);
        _workspace.DisplayDevice = display.DeviceName;
        SaveWorkspace();
        OutputStatusText.Text = test ? "Ventana de ensayo en este monitor" : $"Salida HDMI · {display.Width} × {display.Height}";
        UpdateSessionLabels();
        Activate();
    }
    private void CloseOutput()
    {
        var window = _outputWindow; var surface = _output;
        _outputWindow = null; _output = null; _outputDisplay = null;
        LiveMonitor.SetSource(null);
        surface?.SetPaused(true); window?.Close(); surface?.Dispose();
        _isPaused = true;
        if (_ready) UpdateSessionLabels();
    }
    private void DisplayClick(object sender, RoutedEventArgs e)
    {
        var operatorDevice = DisplayService.GetWindowDevice(this);
        var dialog = new DisplayDialog(DisplayService.GetDisplays().Where(d => d.DeviceName != operatorDevice).ToArray(), _workspace.DisplayDevice) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (dialog.CloseOutput) { CloseOutput(); SetStatus("Salida de proyección cerrada."); return; }
        if (dialog.SelectedDisplay is null) return;
        var display = DisplayService.GetDisplays().FirstOrDefault(d => d.DeviceName == dialog.SelectedDisplay.DeviceName
            && d.DeviceName != DisplayService.GetWindowDevice(this));
        if (display is null) { SetStatus("La pantalla seleccionada ya no está disponible.", true); return; }
        OpenOutput(display, test: false);
        SetStatus("Salida abierta.");
    }
    private void ToggleOutputClick(object sender, RoutedEventArgs e)
    {
        if (_outputWindow is not null) { CloseOutput(); SetStatus("Salida desactivada."); }
        else if (EnsureOutput()) SetStatus("Salida activada.");
    }
    private void NextClick(object sender, RoutedEventArgs e) => MoveLive(1);
    private void PreviousClick(object sender, RoutedEventArgs e) => MoveLive(-1);
    private void MoveLive(int delta)
    {
        if (_outputWindow is null || _session.Live is null) { SetStatus("Proyecta primero un elemento del culto."); return; }
        _workspace.Plan.Items = _plan.ToList();
        var index = _plan.ToList().FindIndex(c => c.Id == _session.Live.Id);
        if (index >= 0)
        {
            var nextIndex = index + delta;
            if (nextIndex < 0 || nextIndex >= _plan.Count) { SetStatus("Has llegado al límite del orden de culto."); return; }
            if (!CheckMedia(_plan[nextIndex])) return;
            if (!_session.Move(_workspace.Plan, delta)) return;
            _preparedReading = null;
            _liveReading = null;
        }
        else if (_liveReading is not null)
        {
            var currentReadingIndex = _liveReading.ToList().FindIndex(c => c.Id == _session.Live.Id);
            if (currentReadingIndex < 0) { SetStatus("El contenido en vivo no pertenece a esta lectura. Prepara un pasaje y pulsa Proyectar."); return; }
            var readingIndex = currentReadingIndex + delta;
            if (readingIndex < 0 || readingIndex >= _liveReading.Count) { SetStatus("Has llegado al final de la lectura preparada."); return; }
            _preparedReading = _liveReading;
            _session.Prepare(_liveReading[readingIndex]); _session.GoLive();
        }
        else { SetStatus("Este contenido no pertenece al orden. Prepara el siguiente y pulsa Proyectar."); return; }
        _output!.ShowCue(_session.Live); _isPaused = false;
        _preview.ShowCue(_session.Prepared);
        _previewPaused = false;
        PreviewPauseButton.Content = "Pausar previa";
        UpdateSessionLabels();
        SetStatus("En pantalla: " + _session.Live!.Title);
    }
    private void BlackoutClick(object sender, RoutedEventArgs e) => ToggleBlackout();
    private void ToggleBlackout()
    {
        if (_output is null) { SetStatus("La salida ya está cerrada."); return; }
        _session.ToggleBlackout();
        _output.ShowCue(_session.Live, _session.IsBlackout, _session.IsTextHidden);
        _isPaused = true;
        UpdateSessionLabels();
        SetStatus(_session.IsBlackout ? "Salida en negro; reproducción pausada." : "Imagen restaurada; reproducción pausada.");
    }
    private void BackgroundClick(object sender, RoutedEventArgs e) => ShowBackground();
    private void ShowBackground()
    {
        if (_output is null) return;
        _session.ShowBackground();
        _output.ShowCue(_session.Live, _session.IsBlackout, _session.IsTextHidden);
        _isPaused = true; UpdateSessionLabels();
        SetStatus("Fondo sin contenido.");
    }
    private void PauseClick(object sender, RoutedEventArgs e) => TogglePause();
    private void TogglePause()
    {
        if (_output is null || _session.Live?.Kind is not (CueKind.Video or CueKind.Audio)) return;
        if (_session.IsBlackout || _session.IsTextHidden) { SetStatus("Restaura la imagen antes de continuar el video."); return; }
        _isPaused = _output.IsPlaying;
        _output.SetPaused(_isPaused);
        PauseButton.Content = _isPaused ? "Continuar" : "Pausar";
    }
    private void RestartClick(object sender, RoutedEventArgs e) => RestartMedia();
    private void RestartMedia()
    {
        if (_output is null || _session.Live?.Kind is not (CueKind.Video or CueKind.Audio)) return;
        if (_session.IsBlackout || _session.IsTextHidden) { SetStatus("Restaura la imagen antes de reiniciar el video."); return; }
        _output.Restart(); _isPaused = false;
    }
    private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeText is not null) VolumeText.Text = $"{(int)e.NewValue} %";
        if (_output is not null) _output.Volume = (int)e.NewValue;
        if (_ready) SaveWorkspace();
    }
    private void LoopChanged(object sender, RoutedEventArgs e)
    { if (_output is not null) _output.Loop = LoopCheck.IsChecked == true; if (_ready) SaveWorkspace(); }
    private void PositionSeek(object sender, MouseButtonEventArgs e)
    { if (_output is not null) _output.Seek(PositionSlider.Value); }
    private static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) seconds = 0;
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"mm\:ss");
    }
    private void TimerTick(object? sender, EventArgs e)
    {
        if (_session.Prepared?.Kind is CueKind.Video or CueKind.Audio)
        {
            var duration = _preview.Duration;
            PreviewTimeText.Text = $"{FormatTime(_preview.Position)} / {FormatTime(duration)}";
            PreviewPositionSlider.IsEnabled = duration > 0;
            if (!PreviewPositionSlider.IsMouseCaptureWithin)
                PreviewPositionSlider.Value = duration > 0 ? Math.Clamp(_preview.Position / duration, 0, 1) : 0;
        }
        if (_output is not null)
        {
            var duration = _output.Duration; var position = _output.Position;
            TimeText.Text = $"{FormatTime(position)} / {FormatTime(duration)}";
            if (!PositionSlider.IsMouseCaptureWithin) PositionSlider.Value = duration > 0 ? Math.Clamp(position / duration, 0, 1) : 0;
            if (_session.Live?.Kind is CueKind.Video or CueKind.Audio)
            { _isPaused = !_output.IsPlaying; PauseButton.Content = _isPaused ? "Continuar" : "Pausar"; }
        }
        if (++_displayTick < 10) return;
        _displayTick = 0;
        if (_outputDisplay is not null && !DisplayService.GetDisplays().Any(d => d.DeviceName == _outputDisplay.DeviceName))
        { CloseOutput(); SetStatus("Se desconectó la pantalla de salida. Conéctala y selecciónala de nuevo.", true); }
    }
}
