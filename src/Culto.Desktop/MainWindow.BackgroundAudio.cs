using System.Windows;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void KeepMediaAudioChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _workspace.KeepMediaAudio = KeepMediaAudioCheck.IsChecked == true;
        if (_output is not null) _output.KeepMediaAudio = _workspace.KeepMediaAudio;
        UpdateBackgroundAudioState(); UpdateUsabilityState(); SaveWorkspace();
    }

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
        BackgroundMediaText.Visibility = background ? Visibility.Visible : Visibility.Collapsed;
        BackgroundMediaText.Text = background ? "Audio: " + _output!.RetainedMediaCue?.Title : "";
        BackgroundMediaText.ToolTip = BackgroundMediaText.Text;
        MediaPlaybackPanel.Visibility = _output is not null && (background
            || _session.Live?.Kind is Culto.Core.CueKind.Video or Culto.Core.CueKind.Audio or Culto.Core.CueKind.WindowCapture)
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
