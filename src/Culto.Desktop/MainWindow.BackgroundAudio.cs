using System.Windows;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool HasBackgroundFileAudio => _output?.HasBackgroundMedia == true
        && _output.AudioCue?.Kind is Culto.Core.CueKind.Video or Culto.Core.CueKind.Audio;
    private bool CanControlFileMedia => HasBackgroundFileAudio || _output is not null
        && !_session.IsBlackout && !_session.IsTextHidden
        && _session.Live?.Kind is Culto.Core.CueKind.Video or Culto.Core.CueKind.Audio;
    private bool HasVisibleMediaOutput => CanControlFileMedia || _output is not null
        && !_session.IsBlackout && !_session.IsTextHidden && _session.Live?.Kind == Culto.Core.CueKind.WindowCapture;
    private string VisualChannelTitle => _output is null ? "Salida cerrada" : _session.IsBlackout ? "Negro"
        : _session.IsTextHidden ? "Logo" : _session.Live?.Title ?? "Fondo";
    private string AudioChannelTitle => _output?.AudioCue?.Title ?? "Sin audio";

    private void ReturnToMediaClick(object sender, RoutedEventArgs e) => ReturnToMedia();
    private void ReturnToMedia()
    {
        if (_output?.HasBackgroundMedia != true || _output.RetainedMediaCue is not { } cue) return;
        PrepareCue(cue); ProjectPrepared();
    }

    private void UpdateBackgroundAudioState()
    {
        if (!_ready) return;
        var background = _output?.HasBackgroundMedia == true;
        ReturnToMediaButton.Visibility = background ? Visibility.Visible : Visibility.Collapsed;
        ReturnToMediaButton.IsEnabled = background;
        BackgroundMediaText.Visibility = _output is not null ? Visibility.Visible : Visibility.Collapsed;
        BackgroundMediaText.Text = "Imagen: " + VisualChannelTitle + " · Audio: " + AudioChannelTitle;
        BackgroundMediaText.ToolTip = BackgroundMediaText.Text;
        MediaPlaybackPanel.Visibility = HasVisibleMediaOutput
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
