using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunSplitControlsTests(MainWindow window)
    {
        RunStopBackgroundMediaTests(window);
        void UntilSplit(Func<bool> ready, string failure) { var deadline = DateTime.UtcNow.AddSeconds(30); while (!ready() && DateTime.UtcNow < deadline) PumpFor(50); Check(ready(), failure); }
        Test("One-shot image and audio commands keep logo and restore the same playing file", () =>
        {
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var session = Field<PresentationSession>(window, "_session");
            ((Slider)window.FindName("VolumeSlider")).Value = 0;
            var logo = Path.Combine(Field<WorkspaceStore>(window, "_store").DirectoryPath, "split-logo.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgr32, null, new byte[16], 8)));
            using (var file = File.Create(logo)) encoder.Save(file);
            workspace.BackgroundImages = [logo]; workspace.BackgroundImagePath = logo;
            var clip = new Cue { Kind = CueKind.Video, Title = "Himno para audio", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
            Call(window, "PrepareCue", clip, null); Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            output.PlaybackError += message => Console.WriteLine("PLAYBACK ERROR " + message);
            try { UntilSplit(() => !output.IsTransitioning && output.IsPlaying, "Initial video never started"); }
            finally { Console.WriteLine($"START displayed={output.DisplayedCue?.Title}, transition={output.IsTransitioning}, playing={output.IsPlaying}, pause={output.IsPaused}, position={output.Position}, status={((TextBlock)window.FindName("StatusText")).Text}"); }
            Call(window, "ShowLogoPreservingAudio");
            UntilSplit(() => !output.IsTransitioning && output.HasBackgroundMedia && session.IsTextHidden, "Visual-only logo lost playing audio");
            var playing = output.ActiveSurface;
            Check(output.IsPlaying, "Logo paused the preserved file");
            Call(window, "ShowLogo");
            UntilSplit(() => !output.IsTransitioning && !output.HasBackgroundMedia, "Normal repeat Logo retained audio");
            Call(window, "PrepareCue", clip, null); Call(window, "ProjectPreparedPreservingOther");
            UntilSplit(() => !output.IsTransitioning && output.HasBackgroundMedia && output.IsPlaying, "Audio-only under repeated logo failed");
            playing = output.ActiveSurface;
            Call(window, "ReturnToMedia");
            UntilSplit(() => !output.IsTransitioning && !session.IsTextHidden, "F9-equivalent did not restore file picture");
            Check(ReferenceEquals(playing, output.ActiveSurface), "Return restarted the decoder");
            Call(window, "ShowLogo");
            UntilSplit(() => !output.IsTransitioning && !output.HasBackgroundMedia, "Normal logo retained audio after one-shot command");
            Call(window, "PrepareCue", clip, null);
            Call(window, "ProjectPreparedPreservingOther");
            UntilSplit(() => !output.IsTransitioning && output.HasBackgroundMedia && output.IsPlaying, "Audio-only selected file never started");
            Check(session.IsTextHidden, "Audio-only file removed the logo from the visual session");
            Call(window, "UpdateBackgroundAudioState");
            Check(((TextBlock)window.FindName("BackgroundMediaText")).Text.Contains("Himno para audio"), "Separate audio label missing");
            Check(((FrameworkElement)window.FindName("MediaPlaybackPanel")).Visibility == Visibility.Visible, "Logo background audio has no volume/transport");
            Call(window, "TogglePause"); Check(output.IsPaused, "Background audio pause ignored under Logo");
            Call(window, "TogglePause"); Check(!output.IsPaused, "Background audio resume ignored under Logo");
            var snapshot = (Culto.Remote.RemoteSnapshot)Call(window, "BuildRemoteState")!;
            Check(snapshot.CanReturnToMedia && snapshot.AudioTitle == clip.Title && snapshot.VisualTitle == "Logo" && snapshot.CanPause,
                "Phone snapshot omitted split channels or transport");
            var reading = new Cue { Kind = CueKind.Bible, Title = "Lectura", Body = "Texto de prueba" };
            Call(window, "PrepareCue", reading, null); Call(window, "ProjectPreparedPreservingOther");
            UntilSplit(() => !output.IsTransitioning && output.DisplayedCue?.Id == reading.Id, "Visual-only reading never appeared");
            Check(output.HasBackgroundMedia, "Reading lost audio selected beneath logo");
            var blackResult = (Culto.Remote.CommandResult)Call(window, "ExecuteRemote", new Culto.Remote.RemoteCommand("scene", "split-black", ButtonId: "blackout", PreserveOther: true))!;
            UntilSplit(() => !output.IsTransitioning && session.IsBlackout, "Phone split blackout failed");
            Check(blackResult.Success && output.HasBackgroundMedia && output.IsPlaying, "Phone blackout stopped audio");
            var returnResult = (Culto.Remote.CommandResult)Call(window, "ExecuteRemote", new Culto.Remote.RemoteCommand("return-media", "split-return"))!;
            UntilSplit(() => !output.IsTransitioning && !session.IsBlackout && !session.IsTextHidden, "Phone return failed");
            Check(returnResult.Success && output.DisplayedCue?.Id == clip.Id, "Phone did not restore sounding video");
            Call(window, "PrepareCue", new Cue { Title = "Normal", Body = "Audio should end" }, null); Call(window, "ProjectPrepared");
            UntilSplit(() => !output.IsTransitioning && !output.HasBackgroundMedia, "Normal projection inherited prior Shift intent");
            Check(output.AudioCue is null && !output.IsPlaying, "Normal projection left one-shot audio playing");
            Call(window, "CloseOutput");
        });
    }
}
