using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunPlayerControlsTests(MainWindow window)
    {
        var volume = (Slider)window.FindName("VolumeSlider");
        var oldVolume = volume.Value;
        var cue = new Cue { Title = "Reproductor de prueba", Kind = CueKind.Video,
            FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
        try
        {
            Call(window, "CloseOutput"); volume.Value = 0;
            Call(window, "PrepareCue", cue, null); Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            var preview = Field<ProjectionSurface>(window, "_preview");
            Until(() => output.IsPlaying && output.Duration > 0 && preview.Duration > 0 && !output.IsTransitioning,
                "Player test video did not load");
            output.SetPaused(true); preview.SetPaused(true);
            PumpFor(100);

            Test("Player scrubbing preserves the selected position and paused output", () =>
            {
                var slider = (Slider)window.FindName("PositionSlider");
                slider.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                slider.Value = .6;
                Call(window, "TimerTick", null, EventArgs.Empty);
                Check(Math.Abs(slider.Value - .6) < .001, "Playback timer overwrote the drag position");
                slider.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                Until(() => Math.Abs(output.Position / output.Duration - .6) < .15, "Dragging did not seek native output");
                Check(output.IsPaused && !output.IsPlaying, "Seeking resumed paused output");
                Check(!Field<bool>(window, "_outputScrubbing"), "Drag did not release seek state");
                slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Handled = true });
                slider.Value = .35;
                Call(window, "TimerTick", null, EventArgs.Empty);
                Check(Math.Abs(slider.Value - .35) < .001, "A handled track click lost its position to the timer");
                slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Handled = true });
                Until(() => Math.Abs(output.Position / output.Duration - .35) < .15, "Handled track click did not seek");
                Check(!Field<bool>(window, "_outputScrubbing") && !slider.IsMouseCaptured, "Track release retained scrubbing or mouse capture");
            });

            Test("Player skip controls affect only their own renderer", () =>
            {
                preview.Seek(.5); output.Seek(.6); PumpFor(150);
                var previewPosition = preview.Position;
                ((Button)window.FindName("OutputSkipBackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => output.Position < .3, "Output rewind did not clamp to the beginning");
                Check(Math.Abs(preview.Position - previewPosition) < .2 && output.IsPaused, "Output rewind changed the preview or resumed output");
                ((Button)window.FindName("PreviewSkipBackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => preview.Position < .3, "Preview rewind did not clamp to the beginning");
                Check(preview.Volume == 0 && output.Position < .3, "Preview controls changed output or enabled preview audio");
            });

            Test("Player mute restores chosen volume and play controls expose their state", () =>
            {
                volume.Value = 37;
                var mute = (Button)window.FindName("MuteButton");
                mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(volume.Value == 0 && output.Volume == 0 && AutomationProperties.GetName(mute) == "Restaurar volumen", "Mute did not silence output");
                mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(volume.Value == 37 && output.Volume == 37, "Unmute lost the chosen volume");
                Call(window, "TimerTick", null, EventArgs.Empty);
                var pause = (Button)window.FindName("PauseButton");
                Check(AutomationProperties.GetName(pause) == "Reproducir salida", "Paused playback has no accessible play label");
                pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => output.IsPlaying, "Play control did not resume output");
                Check(AutomationProperties.GetName(pause) == "Pausar salida", "Running playback has no accessible pause label");
                pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => !output.IsPlaying, "Pause control did not pause output");
            });
        }
        finally { Call(window, "CloseOutput"); volume.Value = oldVolume; }
    }
}
