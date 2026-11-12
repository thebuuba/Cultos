using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunLocalOutputTests(MainWindow window)
    {
        Test("Local output plays without HDMI or a separate projection window", () =>
        {
            Call(window, "CloseOutput");
            var session = Field<PresentationSession>(window, "_session");
            var video = new Cue { Title = "Himno local", Kind = CueKind.Video, FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
            ((Slider)window.FindName("VolumeSlider")).Value = 0;
            Call(window, "PrepareCue", video, null); Call(window, "ProjectPrepared");
            Check(session.Live?.Id == video.Id, "Missing HDMI blocked sending the hymn to En pantalla");
            Check(Field<ProjectionWindow?>(window, "_outputWindow") is null, "Local projection opened an extra window");
            var output = Field<ProjectionSurface>(window, "_output");
            Until(() => output.IsPlaying && !output.IsTransitioning, "Local video failed to play or finish its dissolve");
            Check(((TextBlock)window.FindName("OutputModeText")).Text == "LOCAL", "Local output is mislabeled as HDMI");
            var monitor = (OutputMonitor)window.FindName("LiveMonitor");
            var snapshot = new RenderTargetBitmap((int)monitor.ActualWidth, (int)monitor.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            snapshot.Render(monitor);
            var pixels = new byte[snapshot.PixelWidth * snapshot.PixelHeight * 4]; snapshot.CopyPixels(pixels, snapshot.PixelWidth * 4, 0);
            Check(Enumerable.Range(0, pixels.Length / 4).Count(i => Math.Max(pixels[i*4], Math.Max(pixels[i*4+1], pixels[i*4+2])) > 100) > pixels.Length / 80,
                "Local monitor did not render actual video pixels");
            var live = session.Live;
            Call(window, "PrepareCue", new Cue { Title = "Otra previa", Body = "Independiente" }, null);
            Check(ReferenceEquals(live, session.Live) && output.IsPlaying, "Preparing stopped local output");
            Call(window, "ToggleBlackout");
            Until(() => !output.IsPlaying && !output.IsTransitioning, "Local blackout did not finish its audio and visual transitions");
            Check(output.IsPaused && session.IsBlackout && ReferenceEquals(live, session.Live),
                "Completed local blackout did not retain the paused live video");
            Call(window, "ToggleBlackout");
            // Play can arrive before restoration finishes. The older paused restore
            // must not override this newer explicit playback request on completion.
            Call(window, "TogglePause");
            Until(() => output.IsPlaying && !output.IsTransitioning, "Local Continue did not survive backdrop restoration");
            Check(!output.IsPaused && !session.IsBlackout && ReferenceEquals(live, session.Live),
                "Backdrop restoration overrode the latest local playback request");
            Call(window, "CloseOutput");
        });
    }
}
