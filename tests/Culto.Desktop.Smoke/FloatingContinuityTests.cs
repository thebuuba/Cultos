using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunFloatingContinuityTests(MainWindow window)
    {
        Test("Closing the floating view preserves the decoder and uninterrupted frame delivery", () =>
        {
            window.Topmost = false;
            window.WindowState = WindowState.Maximized;
            ((Slider)window.FindName("VolumeSlider")).Value = 0;
            ((CheckBox)window.FindName("LoopCheck")).IsChecked = true;
            Call(window, "PrepareCue", new Cue { Kind = CueKind.Video, Title = "Floating continuity",
                FilePath = Environment.GetEnvironmentVariable("CULTO_FLOATING_TEST_FILE")
                    ?? Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
            Call(window, "ProjectPrepared");
            if (Environment.GetEnvironmentVariable("CULTO_FLOATING_EXTERNAL_TEST") == "1")
                Call(window, "OpenOutput", Culto.Desktop.Services.DisplayService.GetDisplays().Last(), true);
            var output = Field<ProjectionSurface>(window, "_output");
            Until(() => output.IsPlaying && !output.IsTransitioning, "Fixture did not start");
            var decoder = output.ActiveSurface;
            var frames = Field<object>(decoder, "_compositedVideo");
            var timer = Stopwatch.StartNew();
            var lastFrame = timer.Elapsed.TotalMilliseconds;
            double gap = 0;
            double renderGap = 0, lastRender = timer.Elapsed.TotalMilliseconds;
            var watched = false;
            var count = 0;
            Action presented = () => { var now = timer.Elapsed.TotalMilliseconds; if (watched) gap = Math.Max(gap, now - lastFrame); lastFrame = now; count++; };
            var frameEvent = frames.GetType().GetEvent("FramePresented")!;
            frameEvent.AddEventHandler(frames, presented);
            EventHandler rendering = (_, _) => { var now = timer.Elapsed.TotalMilliseconds; if (watched) renderGap = Math.Max(renderGap, now - lastRender); lastRender = now; };
            System.Windows.Media.CompositionTarget.Rendering += rendering;
            try
            {
                var nativeClick = Environment.GetEnvironmentVariable("CULTO_FLOATING_NATIVE_CLICK") == "1";
                for (var i = 0; i < (nativeClick ? 1 : 5); i++)
                {
                    Call(window, "OpenOutputPlayer");
                    var floating = Field<Window>(window, "_outputPlayerWindow");
                    PumpFor(350);
                    gap = renderGap = 0; watched = true;
                    var beforeFrames = count;
                    var close = Stopwatch.StartNew();
                    if (nativeClick)
                    {
                        Console.WriteLine("READY FOR NATIVE CLOSE CLICK");
                        var deadline = DateTime.UtcNow.AddSeconds(70);
                        while (floating.IsVisible && DateTime.UtcNow < deadline) PumpFor(20);
                        Check(!floating.IsVisible, "Native close click was not received");
                    }
                    else ((Button)floating.FindName("PlayerClose")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    close.Stop();
                    Check(!floating.IsVisible && Field<Window?>(window, "_outputPlayerWindow") is null,
                        "Closing left a floating view attached");
                    PumpFor(550); watched = false;
                    Console.WriteLine($"CLOSE {i}: {(nativeClick ? "interaction" : "close")}={close.Elapsed.TotalMilliseconds:0.0}ms maxFrameGap={gap:0.0}ms renderGap={renderGap:0.0}ms frames={count - beforeFrames}");
                    Check(output.IsPlaying && ReferenceEquals(output.ActiveSurface, decoder), "Closing changed or paused the decoder");
                    Check(count > beforeFrames, "Closing stopped frame presentation");
                    Check(gap < (nativeClick ? 100 : 150), "Closing stalled frame delivery for " + gap.ToString("0") + " ms");
                }
            }
            finally
            {
                System.Windows.Media.CompositionTarget.Rendering -= rendering;
                frameEvent.RemoveEventHandler(frames, presented); Call(window, "CloseOutput");
            }
        });
    }
}
