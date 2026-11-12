using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Remote;

internal static partial class Program
{
    private static void RunYouTubeControlsTests(MainWindow window)
    {
        var session = Field<PresentationSession>(window, "_session");
        var volume = (Slider)window.FindName("VolumeSlider");
        var keep = (CheckBox)window.FindName("KeepMediaAudioCheck");
        var oldVolume = volume.Value;
        Window? floating = null;
        try
        {
            volume.Value = 0;
            Test("Capture playback has no fake pause in console, floating player or remote", () =>
            {
                Call(window, "EnsureOutput");
                var output = Field<ProjectionSurface>(window, "_output");
                output.SetPaused(false);
                session.RestoreOutput(new Cue { Kind = CueKind.WindowCapture, Title = "YouTube de prueba" }, false, false);
                Call(window, "UpdateUsabilityState");
                var before = output.IsPaused;
                Call(window, "TogglePause");
                Check(output.IsPaused == before, "Capture pause changed output playback");
                var pause = (Button)window.FindName("PauseButton");
                Check(!pause.IsEnabled && pause.Visibility == Visibility.Collapsed, "Console exposes a capture pause button");
                Check(!((RemoteSnapshot)Call(window, "BuildRemoteState")!).CanPause, "Phone advertises capture pause support");
                var result = (CommandResult)Call(window, "ExecuteRemote", new RemoteCommand("pause", Guid.NewGuid().ToString()))!;
                Check(!result.Success, "Phone accepted a fake capture pause");
                Call(window, "OpenOutputPlayer"); floating = Field<Window>(window, "_outputPlayerWindow");
                var floatingPause = (Button)floating.FindName("PlayerPause");
                Check(!floatingPause.IsEnabled && floatingPause.Visibility == Visibility.Collapsed, "Floating player exposes capture pause");
                floating.Close(); floating = null;
                Call(window, "CloseOutput");
            });
            Test("YouTube is its own content tab and old Library capture menu is removed", () =>
            {
                var tabs = (TabControl)window.FindName("ContentTabs");
                Check(tabs.Items.Count == 4 && ((TabItem)tabs.Items[3]).Header?.ToString() == "YouTube", "Dedicated YouTube tab is absent");
                var library = (Button)window.FindName("LibraryMenuButton");
                Check(!library.ContextMenu!.Items.OfType<MenuItem>().Any(item => item.Header?.ToString()?.Contains("Capturar ventana") == true),
                    "Capture still lives in Library menu");
                Call(window, "SelectContentSource", 3);
                Check(tabs.SelectedIndex == 3, "YouTube shortcut did not select its section");
                Check(Field<object?>(Field<ProjectionSurface>(window, "_preview"), "_captureClient") is null,
                    "Opening YouTube started an unrequested capture");
            });
            Test("Keep audio and one-way return work through the actual console actions", () =>
            {
                var video = new Cue { Kind = CueKind.Video, Title = "Conservar este video", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
                keep.IsChecked = true;
                Call(window, "PrepareCue", video, null); Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                Until(() => output.IsPlaying && output.Position > .2 && !output.IsTransitioning, "Video did not start");
                var retained = output.ActiveSurface;
                var position = output.Position;
                var text = new Cue { Kind = CueKind.Text, Title = "Texto encima", Body = "La música continúa" };
                Call(window, "PrepareCue", text, null); Call(window, "ProjectPrepared");
                Until(() => output.HasBackgroundMedia && !output.IsTransitioning, "Console did not retain background media");
                PumpFor(450); Call(window, "TimerTick", null, EventArgs.Empty);
                Check(output.IsPlaying && output.Position > position, "Video stopped while text was shown");
                Check(((Button)window.FindName("ReturnToMediaButton")).Visibility == Visibility.Visible
                    && ((FrameworkElement)window.FindName("MediaPlaybackPanel")).Visibility == Visibility.Visible,
                    "Return action or background volume is missing");
                Call(window, "ReturnToMedia");
                Until(() => session.Live?.Id == video.Id && !output.IsTransitioning, "Return did not show the retained video");
                Check(ReferenceEquals(retained, output.ActiveSurface) && output.Position > position, "Return replaced or restarted the decoder");
                Call(window, "ReturnToMedia");
                Check(session.Live?.Id == video.Id, "Repeated return toggled away from video");
                keep.IsChecked = false;
            });
        }
        finally
        {
            floating?.Close(); Call(window, "DisposeYouTube"); Call(window, "CloseOutput");
            keep.IsChecked = false; volume.Value = oldVolume;
        }
    }
}
