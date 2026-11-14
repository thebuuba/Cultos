using System.Windows;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void ReleaseCaptureAudioClick(object sender, RoutedEventArgs e)
    {
        static bool Controlled(Culto.Core.Cue? cue) => cue?.Kind == Culto.Core.CueKind.WindowCapture
            && cue.Capture is { ControlAudio: true } capture && !Services.YouTubeDiscovery.UsesBrowserAudio(capture);
        if (Controlled(_session.Live)
            || _output?.HasBackgroundMedia == true && Controlled(_output.RetainedMediaCue)
            || Controlled(_session.Prepared) || _output?.IsTransitioning == true)
        { SetStatus("Hay una captura activa en salida o vista previa.", true); return; }
        try { Services.CapturedApplicationAudio.RestoreShared(); SetStatus("Audio de las aplicaciones restaurado."); }
        catch (System.Exception error) { SetStatus(error.Message, true); }
    }
}
