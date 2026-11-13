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
    private static void RunColdPlaybackTests(MainWindow window, bool engineTests = true)
    {
        var standby = Field<ProjectionSurface?>(window, "_standbyOutput");
        Test("Startup prepares a silent output without opening projection windows", () =>
        {
            Check(standby is not null && Field<ProjectionSurface?>(window, "_output") is null,
                "Startup did not keep a separate standby compositor");
            Check(standby!.DisplayedCue is null && !standby.IsPlaying
                && Field<ProjectionWindow?>(window, "_outputWindow") is null,
                "Standby preparation opened or played output");
        });
        if (engineTests)
        {
            Test("Standby playback engine warms silently and preserves the first request during loading", () =>
            {
                var type = typeof(ProjectionSurface).Assembly.GetType("Culto.Desktop.Projection.ProjectionMixer")!;
                using var release = new ManualResetEventSlim(false);
                using var started = new ManualResetEventSlim(false);
                var calls = 0;
                Func<LibVLCSharp.Shared.LibVLC> factory = () =>
                {
                    Interlocked.Increment(ref calls); started.Set(); release.Wait();
                    return (LibVLCSharp.Shared.LibVLC)typeof(ProjectionSurface).GetMethod("CreatePlaybackEngine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, null)!;
                };
                var mixer = (Grid)Activator.CreateInstance(type, new object[] { factory })!;
                var host = new Window { Content = mixer, Width = 500, Height = 300 };
                host.Show();
                try
                {
                    var warmup = type.GetMethod("PreparePlaybackEngine");
                    Check(warmup is not null, "No silent preparation exists before the first projection");
                    warmup!.Invoke(mixer, null);
                    Until(() => started.IsSet, "Standby engine did not start");
                    Check(type.GetProperty("DisplayedCue")!.GetValue(mixer) is null
                        && !(bool)type.GetProperty("IsPlaying")!.GetValue(mixer)!, "Preparation started visible playback");
                    var cue = new Cue { Kind = CueKind.Video, Title = "First pending hymn", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
                    type.GetMethod("TransitionTo")!.Invoke(mixer, new object[] { cue });
                    PumpFor(150);
                    Check(calls == 1, "First request started a second native engine");
                    release.Set();
                    Until(() => (type.GetProperty("DisplayedCue")!.GetValue(mixer) as Cue)?.Id == cue.Id
                        && (bool)type.GetProperty("IsPlaying")!.GetValue(mixer)!, "First request needed another projection after warmup");
                }
                finally { release.Set(); ((IDisposable)mixer).Dispose(); host.Close(); }
            });
            foreach (var failWhileWaiting in new[] { false, true })
            Test($"A failed speculative warmup does not discard the first real projection (pending={failWhileWaiting})", () =>
            {
                var type = typeof(ProjectionSurface).Assembly.GetType("Culto.Desktop.Projection.ProjectionMixer")!;
                using var release = new ManualResetEventSlim(false);
                using var started = new ManualResetEventSlim(false);
                var calls = 0;
                Func<LibVLCSharp.Shared.LibVLC> factory = () =>
                {
                    if (Interlocked.Increment(ref calls) == 1)
                    {
                        started.Set();
                        if (failWhileWaiting) release.Wait();
                        throw new InvalidOperationException("Synthetic warmup failure");
                    }
                    return (LibVLCSharp.Shared.LibVLC)typeof(ProjectionSurface).GetMethod("CreatePlaybackEngine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, null)!;
                };
                var mixer = (Grid)Activator.CreateInstance(type, new object[] { factory })!;
                var host = new Window { Content = mixer, Width = 500, Height = 300 }; host.Show();
                try
                {
                    type.GetMethod("PreparePlaybackEngine")!.Invoke(mixer, null);
                    Until(() => failWhileWaiting ? started.IsSet : Field<Task<LibVLCSharp.Shared.LibVLC>>(mixer, "_initialization").IsFaulted, "Synthetic warmup did not start or fail");
                    var cue = new Cue { Kind = CueKind.Video, Title = "First real request", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
                    type.GetMethod("TransitionTo")!.Invoke(mixer, new object[] { cue });
                    release.Set();
                    Until(() => (type.GetProperty("DisplayedCue")!.GetValue(mixer) as Cue)?.Id == cue.Id
                        && (bool)type.GetProperty("IsPlaying")!.GetValue(mixer)!, "First real request was consumed by the failed warmup");
                    Check(calls == 2, "Recovery kept restarting the playback engine");
                }
                finally { release.Set(); ((IDisposable)mixer).Dispose(); host.Close(); }
            });
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
        }
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
                ((Slider)window.FindName("VolumeSlider")).Value = 0;
                Console.WriteLine($"START first prepare {clock.ElapsedMilliseconds} ms");
                Call(window, "PrepareCue", new Cue { Title = "Primer video", Kind = CueKind.Video, FilePath = file }, null);
                Console.WriteLine($"START first project {clock.ElapsedMilliseconds} ms");
                Call(window, "ProjectPrepared");
                Console.WriteLine($"RETURN first project {clock.ElapsedMilliseconds} ms");
                var output = Field<ProjectionSurface>(window, "_output");
                Check(ReferenceEquals(output, standby), "First request discarded the engine prepared at startup");
                output.PlaybackError += message => Console.WriteLine("PLAYBACK ERROR " + message);
                try { Until(() => output.IsPlaying && output.Position > .3 && !output.IsTransitioning, "First video did not reach moving playback"); }
                finally
                {
                    var mixer = Field<object>(output, "_mixer");
                    Console.WriteLine($"OUTPUT displayed={output.DisplayedCue?.Title} transition={output.IsTransitioning} preparing={output.IsPreparingPlaybackEngine} paused={output.IsPaused} position={output.Position}");
                    foreach (var name in new[] { "_current", "_incoming" })
                        if (Field<ProjectionSurface?>(mixer, name) is { } surface)
                        {
                            var player = Field<LibVLCSharp.Shared.MediaPlayer?>(surface, "_player");
                            Console.WriteLine($"SURFACE {name} cue={surface.DisplayedCue?.Title} frame={typeof(ProjectionSurface).GetProperty("HasVideoFrame", Private)!.GetValue(surface)} paused={surface.IsPaused} playing={surface.IsPlaying} native={player?.State} started={Field<bool>(surface, "_started")} pauseRequested={Field<bool>(surface, "_pauseRequested")}");
                        }
                }
                Console.WriteLine($"PLAYING {clock.ElapsedMilliseconds} ms; maximum UI gap {maximumGap} ms");
                Check(maximumGap < 500, $"First playback blocked the UI for {maximumGap} ms");
                output.SetPaused(true);
            });
        }
        finally { timer.Stop(); Call(window, "CloseOutput"); }
    }
}
