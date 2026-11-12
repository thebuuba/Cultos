using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunOutputPlayerWindowTests(MainWindow window)
    {
        Window? player = null;
        var volume = (Slider)window.FindName("VolumeSlider");
        var previousVolume = volume.Value;
        try
        {
            volume.Value = 0;
            Call(window, "CloseOutput");
            Call(window, "PrepareCue", new Cue { Title = "Salida compartida", Kind = CueKind.Video,
                FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
            Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            Until(() => output.IsPlaying && output.Duration > 0 && !output.IsTransitioning, "Video did not load");
            Test("Output player opens once and mirrors the existing compositor", () =>
            {
                Check(window.GetType().GetMethod("OpenOutputPlayer", Private) is not null, "Output player opening is not implemented");
                Call(window, "OpenOutputPlayer");
                player = Field<Window>(window, "_outputPlayerWindow");
                Call(window, "OpenOutputPlayer");
                Check(ReferenceEquals(player, Field<Window>(window, "_outputPlayerWindow")), "Opening twice created another player");
                var monitor = (OutputMonitor)player.FindName("PlayerMonitor");
                Check(ReferenceEquals(Field<VisualBrush>(monitor, "_localImage").Visual, output), "Player does not mirror the actual output");
                Check(ReferenceEquals(Field<ProjectionSurface>(window, "_output"), output), "Opening replaced the decoder");
            });
            if (player is null) return;
            Button FindButton(string name) => (Button)player.FindName(name);
            Test("Floating pause and seek control the same output", () =>
            {
                FindButton("PlayerPause").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => output.IsPaused && !output.IsPlaying, "Floating pause did not pause output");
                var seek = (Slider)player.FindName("PlayerPosition");
                seek.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                seek.Value = .55;
                Call(window, "TimerTick", null, EventArgs.Empty);
                Check(Math.Abs(seek.Value - .55) < .001, "Timer overwrote floating scrub position");
                seek.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                Until(() => Math.Abs(output.Position / output.Duration - .55) < .15, "Floating seek did not reach the requested position");
                Check(output.IsPaused, "Floating seek unexpectedly resumed video");
            });
            Test("Floating volume and fullscreen preserve playback and window bounds", () =>
            {
                ((Slider)player.FindName("PlayerVolume")).Value = 32;
                Check(volume.Value == 32 && output.Volume == 32, "Floating volume is not synchronized");
                FindButton("PlayerMute").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(volume.Value == 0, "Floating mute did not mute output");
                var bounds = new Rect(player.Left, player.Top, player.Width, player.Height);
                Call(player, "ToggleFullscreen"); PumpFor(100);
                Check(player.WindowStyle == WindowStyle.None, "Fullscreen retained the title bar");
                Call(player, "ToggleFullscreen"); PumpFor(100);
                Check(player.WindowStyle != WindowStyle.None && Math.Abs(player.Width - bounds.Width) < 2
                    && Math.Abs(player.Height - bounds.Height) < 2, "Leaving fullscreen lost window size");
                Check(output.IsPaused, "Fullscreen changed playback");
            });
            Test("Floating controls remain inside a small resizable window", () =>
            {
                player.Width = 480; player.Height = 320; player.UpdateLayout(); PumpFor(80);
                var root = (FrameworkElement)player.Content;
                var viewport = new Rect(root.RenderSize);
                foreach (var name in new[] { "PlayerMonitor", "PlayerPause", "PlayerPosition", "PlayerVolume", "PlayerFullscreen" })
                    CheckWithin((FrameworkElement)player.FindName(name), root, viewport, name);
                Check(player.Owner is null, "Minimizing the console would hide the floating player");
            });
            Test("Floating player follows content changes and closing leaves output playing", () =>
            {
                Call(window, "PrepareCue", new Cue { Title = "Nuevo texto", Kind = CueKind.Text, Body = "Una sola salida" }, null);
                Call(window, "ProjectPrepared");
                Call(window, "TimerTick", null, EventArgs.Empty);
                Check(player.Title.Contains("Nuevo texto"), "Floating title did not follow current output");
                Check(!FindButton("PlayerPause").IsEnabled && !((Slider)player.FindName("PlayerPosition")).IsEnabled,
                    "Text exposed video transport");
                Call(window, "PrepareCue", new Cue { Title = "Video final", Kind = CueKind.Video,
                    FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
                Call(window, "ProjectPrepared");
                Until(() => output.IsPlaying && !output.IsTransitioning, "Final video did not play");
                player.Close(); player = null;
                Check(output.IsPlaying && ReferenceEquals(output, Field<ProjectionSurface>(window, "_output")), "Closing floating window stopped output");
            });
        }
        finally { player?.Close(); Call(window, "CloseOutput"); volume.Value = previousVolume; }
    }
}
