using System.Windows;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool _preparedBibleStyleChanged;
    private void RenderPreparedPreview()
    {
        var cue = WithYouTubeLayout(_session.Prepared);
        _preview.BibleStyle = _workspace.BibleStyle.Normalize();
        PreparedMediaCover.Cue = cue;
        // Prepared video/audio are title covers, never a second VLC player.
        _preview.ShowCue(cue?.Kind is CueKind.Video or CueKind.Audio || (cue?.Kind == CueKind.WindowCapture && _session.PreviewFollowsLive) ? null
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
        if (following && _preview.DisplayedCue?.Kind == CueKind.WindowCapture) _preview.ShowCue(null);
        var mediaCover = !following && _session.Prepared?.Kind is CueKind.Video or CueKind.Audio;
        PreviewHost.Visibility = !following && !mediaCover ? Visibility.Visible : Visibility.Collapsed;
        PreparedMediaCover.Visibility = mediaCover ? Visibility.Visible : Visibility.Collapsed;
        PreviewLiveMonitor.Visibility = following ? Visibility.Visible : Visibility.Collapsed;
        PreviewModeText.Text = following ? "EN PANTALLA" : "PREPARADO";
        FollowLiveButton.Visibility = !following && _output is not null ? Visibility.Visible : Visibility.Collapsed;
        PreparedTitleText.Text = following ? (_output is not null ? _session.Live?.Title : null) ?? "Sin salida"
            : _session.Prepared?.Title ?? "Sin contenido";
        PreviewLiveMonitor.SetLocalSource(following ? _output : null);
        if (following && _powerPoint?.IsRunning == true && LiveMonitor.NativeSource != IntPtr.Zero)
        {
            // The local slide show is an embedded child. Mirror its parent's
            // cropped compositor region rather than starting another Office show.
            if (_outputDisplay is null)
            {
                PreviewLiveMonitor.SetNativeSource(IntPtr.Zero);
                PreviewLiveMonitor.SetMirrorSource(LiveMonitor);
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
    }
}
