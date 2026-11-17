using System.Windows;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool _preparedBibleStyleChanged;
    private bool _holdingBiblePreview;
    private void RenderPreparedPreview()
    {
        var cue = WithYouTubeLayout(_session.Prepared);
        _preview.BibleStyle = _workspace.BibleStyle.Normalize();
        PreparedMediaCover.Cue = cue;
        // Independent video previews use VLC's no-audio mode. Once on air,
        // mirror the output and release the extra decoder.
        _preview.ShowCue(cue is { YouTubeUrl: not null, Capture: null } ? new Cue { Kind = CueKind.Text, Title = cue.Title, Body = "YouTube\n" + cue.Title }
            : cue?.Kind == CueKind.Audio || (cue?.Kind is CueKind.Video or CueKind.WindowCapture or CueKind.BrowserSource && _session.PreviewFollowsLive) ? null
            : cue?.Kind == CueKind.PowerPoint ? PowerPointPreview(cue) : cue);
        SyncPreviewSource();
    }

    private void FollowLivePreview()
    {
        _preparedBibleStyleChanged = false;
        _preparedReading = _liveReading;
        _session.FollowLive();
        RenderPreparedPreview();
    }

    private void FollowLivePreviewClick(object sender, RoutedEventArgs e) => FollowLivePreview();

    private void SyncPreviewSource()
    {
        if (!_ready) return;
        var following = _session.PreviewFollowsLive && !(_preparedBibleStyleChanged && _session.Prepared?.Kind == CueKind.Bible);
        // The prepared verse is already visible. Keep it during the dissolve
        // so following live cannot briefly bring the previous verse back.
        _holdingBiblePreview = following && _session.Prepared?.Kind == CueKind.Bible
            && !_session.IsBlackout && !_session.IsTextHidden && _output?.IsTransitioning == true
            && _preview.DisplayedCue?.Id == _session.Prepared.Id;
        if (following && (_session.Prepared?.Kind is CueKind.Video or CueKind.WindowCapture or CueKind.BrowserSource
            || _preview.DisplayedCue?.Kind is CueKind.Video or CueKind.WindowCapture or CueKind.BrowserSource)) _preview.ShowCue(null);
        var mediaCover = !following && _session.Prepared?.Kind == CueKind.Audio;
        PreviewHost.Visibility = (!following && !mediaCover) || _holdingBiblePreview ? Visibility.Visible : Visibility.Collapsed;
        PreparedMediaCover.Visibility = mediaCover ? Visibility.Visible : Visibility.Collapsed;
        PreviewLiveMonitor.Visibility = following && !_holdingBiblePreview ? Visibility.Visible : Visibility.Collapsed;
        FollowLiveButton.Visibility = !following && _output is not null ? Visibility.Visible : Visibility.Collapsed;
        PreparedTitleText.Text = following ? (_output is not null && _session.IsTextHidden && !_session.IsBlackout ? "Logo"
            : (_output is not null ? _session.Live?.Title : null) ?? "Sin salida")
            : _session.Prepared?.Title ?? "Sin contenido";
        PreviewLiveMonitor.SetLocalSource(following && !_holdingBiblePreview ? _output : null);
        if (following && _powerPoint?.IsRunning == true && !_session.IsBlackout && !_session.IsTextHidden)
        {
            // The local slide show is an embedded child. Mirror its parent's
            // cropped compositor region rather than starting another Office show.
            if (_outputDisplay is null)
            {
                PreviewLiveMonitor.SetNativeSource(IntPtr.Zero);
                PreviewLiveMonitor.SetMirrorSource(LocalPowerPointMonitor);
            }
            else
            {
                PreviewLiveMonitor.SetMirrorSource(null);
                PreviewLiveMonitor.SetNativeSource(_powerPoint.Handle);
            }
        }
        else
        {
            PreviewLiveMonitor.SetMirrorSource(null);
            PreviewLiveMonitor.SetNativeSource(IntPtr.Zero);
        }
        UpdatePlaybackOverlay();
    }
}
