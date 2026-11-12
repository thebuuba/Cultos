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

    private bool PreparePowerPointOutput(Cue cue)
    {
        if (cue.Kind != CueKind.PowerPoint) return true;
        PowerPointPresentation? prepared = null;
        try
        {
            SetStatus("Abriendo PowerPoint…");
            prepared = new PowerPointPresentation();
            prepared.Open(cue.FilePath!);
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
        StopPowerPointOutput(keepPending: true, keepCueRequest: true);
        if (_pendingPowerPoint is null) return;
        if (!_powerPointTimerReady)
        {
            _powerPointTimerReady = true;
            _powerPointTimer.Tick += (_, _) =>
            {
                if (_session.IsBlackout || _session.IsTextHidden) return;
                if (_output?.IsTransitioning == true) return;
                _powerPointTimer.Stop();
                if (_pendingPowerPoint is null || _session.Live?.Kind != CueKind.PowerPoint) return;
                var presentation = _pendingPowerPoint;
                _powerPoint = presentation; _pendingPowerPoint = null;
                var request = _pendingCuePlayback.Token;
                var fadeInMilliseconds = _pendingCuePlayback.Transitions.AudioFadeInMilliseconds;
                try
                {
                    presentation.SetAudioMasterVolume((int)VolumeSlider.Value);
                    presentation.SetAudioGain(0);
                    presentation.Start(null);
                    if (!ReferenceEquals(presentation, _powerPoint) || !_pendingCuePlayback.Matches(request)) return;
                    var intent = _pendingCuePlayback.Complete(request)!.Value;
                    _isPaused = _session.IsBlackout || _session.IsTextHidden || intent.IsPaused;
                    if (intent.Restart) presentation.Restart();
                    if (_isPaused) presentation.SetPaused(true);
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

    private void RoutePowerPointOutput()
    {
        if (_powerPoint?.IsRunning != true) return;
        var hidden = _session.IsBlackout || _session.IsTextHidden;
        LiveMonitor.SetNativeSource(IntPtr.Zero);
        _powerPoint.Place(hidden ? null : _outputDisplay);
        if (hidden)
        {
            LiveMonitor.SetNativeSource(IntPtr.Zero);
            if (_outputWindow is not null && !_outputWindow.IsVisible) _outputWindow.Show();
        }
        else
        {
            _outputWindow?.Hide();
            LiveMonitor.SetNativeSource(_powerPoint.Handle, embedded: _outputDisplay is null);
        }
        SyncPreviewSource();
    }

    private void StopPowerPointOutput(bool keepPending = false, bool keepCueRequest = false)
    {
        CancelPowerPointAudioFade();
        if (!keepCueRequest) _pendingCuePlayback.Cancel();
        _powerPointTimer.Stop();
        if (!keepPending) { _pendingPowerPoint?.Dispose(); _pendingPowerPoint = null; }
        LiveMonitor.SetNativeSource(IntPtr.Zero);
        PreviewLiveMonitor.SetMirrorSource(null);
        PreviewLiveMonitor.SetNativeSource(IntPtr.Zero);
        _powerPoint?.Dispose(); _powerPoint = null;
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
            NextLiveText.Text = $"Diapositiva {_powerPoint.SlideIndex} de {_powerPoint.SlideCount}";
    }

    private static Cue PowerPointPreview(Cue cue)
    {
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
