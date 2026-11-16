using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private PowerPointModeratorWindow? _powerPointModerator;
    private PowerPointPresentation? _moderatorPresentation;
    private int _moderatorSlide;
    private string? _moderatorPreviewPath;
    private OutputMonitor LocalPowerPointMonitor =>
        _powerPointModerator is { IsVisible: true, WindowState: not WindowState.Minimized } view ? view.Monitor : LiveMonitor;

    private void OpenPowerPointModeratorClick(object sender, RoutedEventArgs e)
    {
        if (_session.Live?.Kind != CueKind.PowerPoint || _powerPoint?.IsRunning != true) return;
        if (_powerPointModerator is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }
        var view = new PowerPointModeratorWindow { Owner = this };
        _powerPointModerator = view;
        _moderatorPreviewPath = Path.Combine(Path.GetTempPath(), "CultoStudioModerator-" + Guid.NewGuid().ToString("N") + ".png");
        view.NavigateRequested += NavigatePowerPointCard;
        view.PauseRequested += () => PowerPointPauseClick(this, new RoutedEventArgs());
        view.RestartRequested += () => PowerPointRestartClick(this, new RoutedEventArgs());
        view.BlackoutRequested += () => { if (_session.Live?.Kind == CueKind.PowerPoint) ToggleBlackout(); };
        view.StateChanged += (_, _) => RoutePowerPointOutput();
        view.Closing += (_, _) =>
        {
            // Reparent before WPF destroys the moderator HWND and its children.
            view.Monitor.SetNativeSource(IntPtr.Zero);
            view.Monitor.SetMirrorSource(null);
            _powerPointModerator = null;
            RoutePowerPointOutput();
        };
        view.Closed += (_, _) =>
        {
            _moderatorPresentation = null; _moderatorSlide = 0;
            try { if (_moderatorPreviewPath is { } path) File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* Temporary preview can be reclaimed later. */ }
            _moderatorPreviewPath = null;
        };
        view.Show();
        RoutePowerPointOutput();
    }

    private void UpdatePowerPointModerator()
    {
        if (_powerPointModerator is not { } view) return;
        var presentation = _powerPoint;
        var running = _session.Live?.Kind == CueKind.PowerPoint && presentation?.IsRunning == true;
        var hidden = _session.IsBlackout || _session.IsTextHidden;
        view.UpdateState(_session.Live?.Title ?? "", _powerPointSlideIndex, presentation?.SlideCount ?? 0,
            running, running && !hidden && !_pendingCuePlayback.IsPending, _isPaused, _session.IsBlackout, _session.IsTextHidden);
        view.Monitor.SetLocalSource(running && hidden ? _output : null);
        var hostsNativeSlide = running && !hidden && _outputDisplay is null && ReferenceEquals(LocalPowerPointMonitor, view.Monitor);
        view.Monitor.SetMirrorSource(running && !hidden && _outputDisplay is null && !hostsNativeSlide ? LiveMonitor : null);
        view.Monitor.SetNativeSource(running && !hidden && (_outputDisplay is not null || hostsNativeSlide) ? presentation!.Handle : IntPtr.Zero,
            embedded: hostsNativeSlide);
        if (!running)
        {
            _moderatorPresentation = null; _moderatorSlide = 0;
            return;
        }
        if (_powerPointSlideIndex < 1 || _powerPointSlideIndex > presentation!.SlideCount) return;
        if (ReferenceEquals(_moderatorPresentation, presentation) && _moderatorSlide == _powerPointSlideIndex) return;
        try
        {
            var index = _powerPointSlideIndex;
            var notes = presentation!.ReadSlideNotes(index);
            BitmapImage? next = null;
            if (index < presentation.SlideCount)
            {
                File.Delete(_moderatorPreviewPath!);
                presentation.ExportSlidePreview(index + 1, _moderatorPreviewPath!);
                using var stream = File.OpenRead(_moderatorPreviewPath!);
                next = new BitmapImage();
                next.BeginInit(); next.CacheOption = BitmapCacheOption.OnLoad; next.StreamSource = stream; next.EndInit(); next.Freeze();
            }
            view.SetSlide(notes, next);
            _moderatorPresentation = presentation; _moderatorSlide = index;
        }
        catch (Exception error) when (error is COMException or InvalidComObjectException or IOException or UnauthorizedAccessException)
        {
            // Office can be busy during native transitions. Retry with the next
            // position poll; moderator metadata must never stop live playback.
            view.SetSlide("No se pudo cargar la vista de esta diapositiva. Reintentando…", null);
        }
    }
}
