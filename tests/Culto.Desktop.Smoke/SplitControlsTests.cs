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
        Test("Video scene uses the prepared video and disables without an active or prepared clip", () =>
        {
            Call(window, "CloseOutput");
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var remembered = workspace.QuickSceneVideo;
            var clip = new Cue { Kind = CueKind.Video, Title = "Video preparado", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
            try
            {
                workspace.QuickSceneVideo = clip;
                Call(window, "PrepareCue", new Cue { Title = "Texto", Body = "Lectura" }, null);
                var state = (Culto.Remote.RemoteSnapshot)Call(window, "BuildRemoteState")!;
                Check(!state.Scenes!.Single(scene => scene.Id == "video").Available, "Video scene fell back to an old remembered clip");
                Call(window, "PrepareCue", clip, null);
                state = (Culto.Remote.RemoteSnapshot)Call(window, "BuildRemoteState")!;
                Check(state.Scenes!.Single(scene => scene.Id == "video") is { Available: true, Detail: "Video preparado" },
                    "Video scene did not advertise the prepared video");
                ((Slider)window.FindName("VolumeSlider")).Value = 0;
                var result = (Culto.Remote.CommandResult)Call(window, "ExecuteRemote",
                    new Culto.Remote.RemoteCommand("scene", "scene-start-prepared", ButtonId: "video"))!;
                Check(result.Success && Field<PresentationSession>(window, "_session").Live?.Id == clip.Id, "Video scene did not start the prepared clip");
            }
            finally { Call(window, "CloseOutput"); workspace.QuickSceneVideo = remembered; }
        });
        void UntilSplit(Func<bool> ready, string failure) { var deadline = DateTime.UtcNow.AddSeconds(30); while (!ready() && DateTime.UtcNow < deadline) PumpFor(50); Check(ready(), failure); }
        Test("Shift music shows enabled playback controls without mouse hover", () =>
        {
            var path = Path.Combine(Field<WorkspaceStore>(window, "_store").DirectoryPath, "shift-music.wav");
            using (var writer = new NAudio.Wave.WaveFileWriter(path, new NAudio.Wave.WaveFormat(8000, 16, 1)))
                writer.Write(new byte[160000], 0, 160000);
            try
            {
                Call(window, "CloseOutput");
                Call(window, "PrepareCue", new Cue { Title = "Imagen conservada", Body = "Bienvenidos" }, null);
                Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                UntilSplit(() => !output.IsTransitioning, "Foreground never settled");
                Call(window, "PrepareCue", new Cue { Kind = CueKind.Audio, Title = "Música con Shift", FilePath = path }, null);
                Check((bool)Call(window, "ProjectPreparedPreservingOther")!, "Shift music was rejected");
                UntilSplit(() => output.HasBackgroundMedia && output.IsPlaying && output.Duration > 0, "Shift music did not load");
                Call(window, "ShowPlaybackOverlay", false);
                Call(window, "UpdateBackgroundAudioState");
                var transport = (FrameworkElement)window.FindName("MediaPlaybackPanel");
                UntilSplit(() => transport.IsHitTestVisible && transport.Opacity > .9, "Music player is hidden without mouse hover");
                Check(((FrameworkElement)window.FindName("OutputTransportGrid")).Visibility == Visibility.Visible
                    && ((Button)window.FindName("PauseButton")).IsEnabled, "Music player has no enabled transport after loading");
                Call(window, "PrepareCue", new Cue { Title = "Siguiente lectura", Body = "Otro contenido" }, null);
                Check(transport.IsHitTestVisible, "Preparing another cue hides the background player");
                Call(window, "TogglePause"); Check(output.IsPaused, "Shift music cannot pause");
                Call(window, "TogglePause"); Check(!output.IsPaused, "Shift music cannot resume");
            }
            finally { Call(window, "CloseOutput"); }
        });
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
            var otherVideo = clip.Copy(newId: true); otherVideo.Title = "Otro video preparado";
            Call(window, "PrepareCue", otherVideo, null);
            var videoScene = ((Culto.Remote.RemoteSnapshot)Call(window, "BuildRemoteState")!).Scenes!.Single(scene => scene.Id == "video");
            Check(videoScene.Available && videoScene.Detail == clip.Title, "Video scene did not prefer the already playing clip");
            var position = output.Position;
            var videoResult = (Culto.Remote.CommandResult)Call(window, "ExecuteRemote",
                new Culto.Remote.RemoteCommand("scene", "scene-playing-video", ButtonId: "video"))!;
            UntilSplit(() => !output.IsTransitioning && !session.IsTextHidden, "Video scene did not restore the picture");
            Check(videoResult.Success && ReferenceEquals(playing, output.ActiveSurface) && output.Position >= position - .2,
                "Video scene restarted the playing clip or chose the prepared clip");
            Call(window, "TogglePause");
            position = output.Position;
            Call(window, "ExecuteRemote", new Culto.Remote.RemoteCommand("scene", "scene-repeat-paused", ButtonId: "video"));
            PumpFor(300);
            Check(output.IsPaused && Math.Abs(output.Position - position) < .3, "Tapping active Video resumed a paused clip");
            Call(window, "ShowLogoPreservingAudio");
            UntilSplit(() => !output.IsTransitioning && output.HasBackgroundMedia, "Paused video did not remain behind the logo");
            playing = output.ActiveSurface;
            Call(window, "ExecuteRemote", new Culto.Remote.RemoteCommand("scene", "scene-return-paused", ButtonId: "video"));
            UntilSplit(() => !output.IsTransitioning && !session.IsTextHidden, "Paused video did not return from logo");
            Check(output.IsPaused && ReferenceEquals(playing, output.ActiveSurface), "Returning paused Video resumed or replaced the decoder");
            Call(window, "TogglePause");
            Call(window, "ShowLogoPreservingAudio");
            UntilSplit(() => !output.IsTransitioning && output.HasBackgroundMedia, "Video did not return behind logo");
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
            Call(window, "ShowPlaybackOverlay", false);
            Call(window, "UpdateBackgroundAudioState");
            var transport = (FrameworkElement)window.FindName("MediaPlaybackPanel");
            UntilSplit(() => transport.IsHitTestVisible && transport.Opacity > .9, "Shift video keeps its player hidden until mouse hover");
            Check(ReferenceEquals(Field<ContentControl>(window, "_playbackOverlayHost"), window.FindName("LiveOverlayHost")),
                "Shift playback controls are attached to a different preview");
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
