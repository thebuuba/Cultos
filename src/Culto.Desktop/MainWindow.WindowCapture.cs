using System.Windows;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void ReleaseCaptureAudioClick(object sender, RoutedEventArgs e)
    {
        if (_session.Live?.Kind == Culto.Core.CueKind.WindowCapture
            || _output?.HasBackgroundMedia == true && _output.RetainedMediaCue?.Kind == Culto.Core.CueKind.WindowCapture
            || _session.Prepared?.Kind == Culto.Core.CueKind.WindowCapture || _output?.IsTransitioning == true)
        { SetStatus("Hay una captura activa en salida o vista previa.", true); return; }
        try { Services.CapturedApplicationAudio.RestoreShared(); SetStatus("Audio de las aplicaciones restaurado."); }
        catch (System.Exception error) { SetStatus(error.Message, true); }
    }

    private void CaptureWindowClick(object sender, RoutedEventArgs e)
    {
        var dialog = new WindowCaptureDialog(youTubeLayout: _workspace.YouTubeLayout) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedCue is not { } cue) return;
        AddCaptureToPlan(cue);
    }
}
