using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunColdPlaybackTests(MainWindow window)
    {
        Test("Slow native initialization does not block UI or replay a superseded cue", () =>
        {
            var type = typeof(ProjectionSurface).Assembly.GetType("Culto.Desktop.Projection.ProjectionMixer")!;
            var constructor = type.GetConstructor(new[] { typeof(Func<LibVLCSharp.Shared.LibVLC>) });
            Check(constructor is not null, "Mixer cannot exercise slow asynchronous native initialization");
            using var release = new ManualResetEventSlim(false);
            using var started = new ManualResetEventSlim(false);
            Func<LibVLCSharp.Shared.LibVLC> factory = () =>
            {
                started.Set(); release.Wait();
                return (LibVLCSharp.Shared.LibVLC)typeof(ProjectionSurface).GetMethod("CreatePlaybackEngine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, null)!;
            };
            var mixer = (Grid)constructor!.Invoke(new object[] { factory });
            var surfaceWindow = new Window { Content = mixer, Width = 500, Height = 300 };
            surfaceWindow.Show();
            try
            {
                var method = type.GetMethod("TransitionTo")!;
                var elapsed = Stopwatch.StartNew();
                method.Invoke(mixer, new object[] { new Cue { Kind = CueKind.Video, Title = "Esperando motor", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") } });
                Check(elapsed.ElapsedMilliseconds < 200, "Native initialization blocked the request");
                Until(() => started.IsSet, "Native factory did not start");
                var heartbeatRan = false;
                window.Dispatcher.BeginInvoke(() => heartbeatRan = true);
                PumpFor(100); Check(heartbeatRan, "Dispatcher stopped while native initialization was pending");
                var text = new Cue { Kind = CueKind.Text, Title = "Solicitud nueva", Body = "Conservar esta salida" };
                method.Invoke(mixer, new object[] { text });
                Until(() => (type.GetProperty("DisplayedCue")!.GetValue(mixer) as Cue)?.Id == text.Id, "New text did not replace the pending request");
                release.Set(); PumpFor(700);
                Check((type.GetProperty("DisplayedCue")!.GetValue(mixer) as Cue)?.Id == text.Id, "Delayed initialization replayed the discarded video");
            }
            finally { release.Set(); ((IDisposable)mixer).Dispose(); surfaceWindow.Close(); }
        });
        Test("Pause and close remain usable while the native engine is loading", () =>
        {
            var type = typeof(ProjectionSurface).Assembly.GetType("Culto.Desktop.Projection.ProjectionMixer")!;
            using var release = new ManualResetEventSlim(false);
            Func<LibVLCSharp.Shared.LibVLC> factory = () =>
            {
                release.Wait();
                return (LibVLCSharp.Shared.LibVLC)typeof(ProjectionSurface).GetMethod("CreatePlaybackEngine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, null)!;
            };
            var mixer = (Grid)Activator.CreateInstance(type, new object[] { factory })!;
            var surfaceWindow = new Window { Content = mixer, Width = 500, Height = 300 }; surfaceWindow.Show();
            try
            {
                type.GetMethod("TransitionTo")!.Invoke(mixer, new object[] { new Cue { Kind = CueKind.Video, FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") } });
                type.GetMethod("SetPaused")!.Invoke(mixer, new object[] { true });
                release.Set();
                Until(() => type.GetProperty("DisplayedCue")!.GetValue(mixer) is Cue, "Paused loading cue was lost");
                Check((bool)type.GetProperty("IsPaused")!.GetValue(mixer)!, "Initialization lost the pause requested while waiting");
            }
            finally { release.Set(); ((IDisposable)mixer).Dispose(); surfaceWindow.Close(); }
            using var closeGate = new ManualResetEventSlim(false);
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<LibVLCSharp.Shared.LibVLC> blockedFactory = () =>
            {
                closeGate.Wait(); finished.SetResult(); throw new InvalidOperationException("Cierre de prueba");
            };
            var pending = (IDisposable)Activator.CreateInstance(type, new object[] { blockedFactory })!;
            type.GetMethod("TransitionTo")!.Invoke(pending, new object[] { new Cue { Kind = CueKind.Video, FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") } });
            var elapsed = Stopwatch.StartNew(); pending.Dispose();
            Check(elapsed.ElapsedMilliseconds < 200, "Closing waited for native initialization");
            closeGate.Set(); Until(() => finished.Task.IsCompleted, "Pending initialization was not released"); PumpFor(50);
        });
        var file = Environment.GetEnvironmentVariable("CULTO_FIRST_PLAY_FILE")
            ?? Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv");
        var clock = Stopwatch.StartNew();
        long heartbeat = 0, maximumGap = 0;
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(25), DispatcherPriority.Normal, (_, _) =>
        {
            var now = clock.ElapsedMilliseconds;
            maximumGap = Math.Max(maximumGap, now - heartbeat); heartbeat = now;
        }, window.Dispatcher);
        using var watchdog = new System.Threading.Timer(_ =>
        {
            if (clock.ElapsedMilliseconds - System.Threading.Interlocked.Read(ref heartbeat) > 2000)
                Console.WriteLine($"STALL: {clock.ElapsedMilliseconds} ms; last UI heartbeat {heartbeat} ms; PID {Environment.ProcessId}");
        }, null, 2500, 2000);
        try
        {
            PumpFor(100);
            Test("First playback keeps the UI responsive and reaches moving video", () =>
            {
                Console.WriteLine($"START first prepare {clock.ElapsedMilliseconds} ms");
                Call(window, "PrepareCue", new Cue { Title = "Primer video", Kind = CueKind.Video, FilePath = file }, null);
                Console.WriteLine($"START first project {clock.ElapsedMilliseconds} ms");
                Call(window, "ProjectPrepared");
                Console.WriteLine($"RETURN first project {clock.ElapsedMilliseconds} ms");
                var output = Field<ProjectionSurface>(window, "_output");
                Until(() => output.IsPlaying && output.Position > .3 && !output.IsTransitioning, "First video did not reach moving playback");
                Console.WriteLine($"PLAYING {clock.ElapsedMilliseconds} ms; maximum UI gap {maximumGap} ms");
                Check(maximumGap < 500, $"First playback blocked the UI for {maximumGap} ms");
                output.SetPaused(true);
            });
        }
        finally { timer.Stop(); Call(window, "CloseOutput"); }
    }
}
