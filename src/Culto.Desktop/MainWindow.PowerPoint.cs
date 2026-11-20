using System.IO.Compression;
using System.Windows;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private PowerPointPresentation? _powerPoint, _pendingPowerPoint;
    private readonly DispatcherTimer _powerPointTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private bool _powerPointTimerReady;
    private Cue? _powerPointCue, _powerPointEntryFrame;
    private bool _powerPointOnAir, _powerPointWaitingForTransition, _powerPointResumePaused;
    private readonly List<string> _powerPointFrameFiles = [];

    private bool PreparePowerPointOutput(Cue cue)
    {
        if (cue.Kind != CueKind.PowerPoint) return true;
        PowerPointPresentation? prepared = null;
        try
        {
            if (_powerPoint?.IsRunning == true && SamePowerPoint(cue, _powerPointCue))
            {
                if (_powerPointOnAir) _powerPointResumePaused = _powerPoint.IsPaused;
                _pendingPowerPoint?.Dispose(); _pendingPowerPoint = null;
                _powerPointEntryFrame = PowerPointFrame(_powerPoint, cue, _powerPoint.SlideIndex);
                return true;
            }
            _powerPointRequestedStart = SamePowerPoint(cue, _currentPowerPointFile) ? _powerPointCursor : 1;
            SetStatus("Abriendo PowerPoint…");
            prepared = new PowerPointPresentation();
            prepared.Open(cue.FilePath!);
            _powerPointEntryFrame = PowerPointFrame(prepared, cue, Math.Clamp(_powerPointRequestedStart, 1, prepared.SlideCount));
            _pendingPowerPoint?.Dispose(); _pendingPowerPoint = prepared;
            return true;
        }
        catch (Exception error)
        {
            prepared?.Dispose();
            SetStatus("No se pudo abrir PowerPoint. " + error.Message, true);
            return false;
        }
    }

    private void ShowPowerPointOutput()
    {
        if (_pendingPowerPoint is null && _powerPoint?.IsRunning != true) return;
        _powerPointWaitingForTransition = true;
        if (!_powerPointTimerReady)
        {
            _powerPointTimerReady = true;
            _powerPointTimer.Tick += (_, _) =>
            {
                if (_session.IsBlackout || _session.IsTextHidden) return;
                if (_output?.IsTransitioning == true) return;
                _powerPointTimer.Stop();
                if (_session.Live?.Kind != CueKind.PowerPoint) return;
                var starting = _pendingPowerPoint is not null;
                var presentation = _pendingPowerPoint ?? _powerPoint;
                if (presentation is null) return;
                var request = _pendingCuePlayback.Token;
                var fadeInMilliseconds = _pendingCuePlayback.Transitions.AudioFadeInMilliseconds;
                try
                {
                    if (starting)
                    {
                        ReleasePowerPointMonitors();
                        _powerPoint?.Dispose();
                        _powerPoint = presentation; _pendingPowerPoint = null;
                        _powerPointResumePaused = false;
                    }
                    presentation.SetAudioMasterVolume((int)VolumeSlider.Value);
                    presentation.SetAudioGain(0);
                    if (starting)
                    {
                        presentation.Start(null);
                        if (_powerPointRequestedStart > 1) presentation.GoToSlide(Math.Min(_powerPointRequestedStart, presentation.SlideCount));
                    }
                    if (!ReferenceEquals(presentation, _powerPoint) || request != _pendingCuePlayback.Token) return;
                    var intent = _pendingCuePlayback.Complete(request) ?? new CuePlaybackIntent(true, false);
                    _isPaused = _session.IsBlackout || _session.IsTextHidden || intent.IsPaused;
                    if (intent.Restart) presentation.Restart();
                    presentation.SetPaused(_isPaused);
                    _powerPointCue = _session.Live!.Copy();
                    _powerPointWaitingForTransition = false;
                    PowerPointPauseButton.Content = _isPaused ? "Continuar" : "Pausar";
                    RoutePowerPointOutput();
                    UpdatePowerPointPosition();
                    if (!_isPaused) FadePowerPointIn(fadeInMilliseconds);
                }
                catch (Exception error) { StopPowerPointOutput(); SetStatus("PowerPoint: " + error.Message, true); }
            };
        }
        _powerPointTimer.Start();
    }

    private Cue PowerPointFrame(PowerPointPresentation presentation, Cue cue, int slide)
    {
        var path = Path.Combine(Path.GetTempPath(), "CultoStudioPowerPoint-" + Guid.NewGuid().ToString("N") + ".png");
        presentation.ExportTransitionFrame(slide, path);
        _powerPointFrameFiles.Add(path);
        return new Cue { Id = cue.Id, Kind = CueKind.Image, Title = cue.Title, FilePath = path };
    }

    private void ParkPowerPointOutput()
    {
        _powerPointTimer.Stop();
        if (_session.Live?.Kind != CueKind.PowerPoint)
        { _pendingPowerPoint?.Dispose(); _pendingPowerPoint = null; _powerPointEntryFrame = null; _powerPointWaitingForTransition = false; }
        if (_powerPoint?.IsRunning != true) return;
        try
        {
            if (_powerPointOnAir && _powerPointCue is { } cue)
            {
                // Seed the compositor before detaching the native HWND.
                _powerPointResumePaused = _powerPoint.IsPaused;
                var frame = PowerPointFrame(_powerPoint, cue, _powerPoint.SlideIndex);
                _output?.ShowCue(frame);
                if (_pendingPowerPoint is null) _powerPointEntryFrame = frame;
            }
        }
        catch (Exception error) { SetStatus("No se pudo capturar el fotograma de PowerPoint: " + error.Message, true); }
        try
        {
            if (_outputWindow is not null && !_outputWindow.IsVisible) _outputWindow.Show();
            ReleasePowerPointMonitors();
            _powerPoint.Place(null); _powerPointOnAir = false;
            _powerPoint.SetPaused(true); _powerPoint.SetAudioGain(0);
        }
        catch (Exception error)
        {
            StopPowerPointOutput(keepPending: true, keepCueRequest: true);
            SetStatus("PowerPoint se cerró al ocultar su ventana: " + error.Message, true);
        }
    }

    private void ClearPowerPointFrames()
    {
        foreach (var path in _powerPointFrameFiles)
            try { File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        _powerPointFrameFiles.Clear();
    }

    private void ReleasePowerPointMonitors()
    {
        LiveMonitor.SetMirrorSource(null); LiveMonitor.SetNativeSource(IntPtr.Zero);
        _powerPointModerator?.Monitor.SetMirrorSource(null); _powerPointModerator?.Monitor.SetNativeSource(IntPtr.Zero);
        PreviewLiveMonitor.SetMirrorSource(null); PreviewLiveMonitor.SetNativeSource(IntPtr.Zero);
    }

    private void RoutePowerPointOutput()
    {
        if (_powerPoint?.IsRunning != true) return;
        var hidden = _session.Live?.Kind != CueKind.PowerPoint || _session.IsBlackout || _session.IsTextHidden || _powerPointWaitingForTransition;
        LiveMonitor.SetMirrorSource(null);
        LiveMonitor.SetNativeSource(IntPtr.Zero);
        _powerPointModerator?.Monitor.SetMirrorSource(null);
        _powerPointModerator?.Monitor.SetNativeSource(IntPtr.Zero);
        _powerPoint.Place(hidden ? null : _outputDisplay);
        _powerPointOnAir = !hidden;
        if (hidden)
        {
            LiveMonitor.SetNativeSource(IntPtr.Zero);
            if (_outputWindow is not null && !_outputWindow.IsVisible) _outputWindow.Show();
        }
        else
        {
            _outputWindow?.Hide();
            if (_outputDisplay is null)
            {
                // Render at the moderator's full size. The smaller console and
                // preview mirror this native surface instead of enlarging theirs.
                var host = LocalPowerPointMonitor;
                host.SetNativeSource(_powerPoint.Handle, embedded: true);
                if (!ReferenceEquals(host, LiveMonitor)) LiveMonitor.SetMirrorSource(host);
            }
            else LiveMonitor.SetNativeSource(_powerPoint.Handle);
        }
        SyncPreviewSource();
        UpdatePowerPointModerator();
    }

    private void StopPowerPointOutput(bool keepPending = false, bool keepCueRequest = false)
    {
        CancelPowerPointAudioFade();
        if (!keepCueRequest) _pendingCuePlayback.Cancel();
        _powerPointTimer.Stop();
        if (!keepPending) { _pendingPowerPoint?.Dispose(); _pendingPowerPoint = null; }
        LiveMonitor.SetMirrorSource(null);
        LiveMonitor.SetNativeSource(IntPtr.Zero);
        _powerPointModerator?.Monitor.SetMirrorSource(null);
        _powerPointModerator?.Monitor.SetNativeSource(IntPtr.Zero);
        PreviewLiveMonitor.SetMirrorSource(null);
        PreviewLiveMonitor.SetNativeSource(IntPtr.Zero);
        _powerPoint?.Dispose(); _powerPoint = null;
        _powerPointCue = null;
        if (!keepPending) _powerPointEntryFrame = null;
        _powerPointOnAir = _powerPointWaitingForTransition = _powerPointResumePaused = false;
        UpdatePowerPointModerator();
        if (_outputWindow is not null && !_outputWindow.IsVisible) _outputWindow.Show();
    }

    private void AdvancePresentation(int direction)
    {
        if (_closing) return;
        if (_session.Live?.Kind == CueKind.PowerPoint)
        {
            if (_session.IsBlackout || _session.IsTextHidden) return;
            try
            {
                if (AdvancePowerPointWithFade(direction)) UpdatePowerPointPosition();
                else SetStatus("Sin más pasos en esa dirección.");
            }
            catch (Exception error) { StopPowerPointOutput(); SetStatus("PowerPoint: " + error.Message, true); }
            return;
        }
        if (_output is null || _session.Live is null)
        {
            if (_session.Prepared is null && _plan.Count > 0) PrepareCue(_plan[0]);
            ProjectPreparedWithoutActivation();
        }
        else MoveLive(direction);
    }

    private void UpdatePowerPointPosition()
    {
        if (_powerPoint?.IsRunning == true)
        {
            _powerPointSlideIndex = _powerPoint.SlideIndex;
            NextLiveText.Text = $"Diapositiva {_powerPointSlideIndex} de {_powerPoint.SlideCount}";
        }
        UpdatePowerPointPanel();
    }

    private Cue PowerPointPreview(Cue cue)
    {
        if (SamePowerPoint(cue, _currentPowerPointFile)
            && _powerPointSlidePreviews.ElementAtOrDefault(_powerPointCursor - 1)?.ImagePath is { } previewPath)
            return new Cue { Id = cue.Id, Kind = CueKind.Image, Title = cue.Title, FilePath = previewPath };
        try
        {
            if (cue.FilePath is null) return cue;
            using var zip = ZipFile.OpenRead(cue.FilePath);
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("docProps/thumbnail.", StringComparison.OrdinalIgnoreCase)
                && Path.GetExtension(e.FullName).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg");
            if (entry is null || entry.Length > 10_000_000) return cue;
            var folder = Path.Combine(Path.GetTempPath(), "CultoStudioPreviews");
            Directory.CreateDirectory(folder);
            var destination = Path.Combine(folder, cue.Id.ToString("N") + Path.GetExtension(entry.Name));
            entry.ExtractToFile(destination, overwrite: true);
            return new Cue { Id = cue.Id, Kind = CueKind.Image, Title = cue.Title, FilePath = destination };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException) { return cue; }
    }
}
