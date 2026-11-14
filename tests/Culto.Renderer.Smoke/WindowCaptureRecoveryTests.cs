using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    // Exercises only an owned animated source and cover; no user's window is captured.
    private static async Task RunWindowCaptureRecoveryChecks()
    {
        Check(ObsWindowCaptureClient.IsAvailable, "Recovery suite has the isolated native capture runtime");
        var start = new ProcessStartInfo(Path.GetFullPath(".tools/dotnet/dotnet.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Environment.CurrentDirectory
        };
        start.Environment["CULTO_CAPTURE_SOURCE_SILENT"] = "1";
        start.ArgumentList.Add(typeof(Program).Assembly.Location); start.ArgumentList.Add("--capture-source");
        using var source = Process.Start(start) ?? throw new Exception("Cannot start capture fixture.");
        using var surface = new ProjectionSurface { Volume = 60 };
        var host = new Window { Title = "Culto recovery output", Content = surface,
            Left = 600, Top = 50, Width = 520, Height = 330, ShowInTaskbar = false };
        var cover = new Window { Title = "Culto recovery maximize cover", Background = Brushes.Magenta,
            Left = 20, Top = 20, Width = 600, Height = 400, Topmost = true, ShowInTaskbar = false };
        var errors = new List<string>(); surface.PlaybackError += errors.Add;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(object instance, string name) => instance.GetType().GetField(name, flags)!.GetValue(instance);
        BitmapSource? Bitmap() => (BitmapSource?)Field(surface, "_captureBitmap");
        Process? Worker() => Field(surface, "_captureClient") is { } client ? (Process?)Field(client, "_worker") : null;
        int Pixel()
        {
            var bitmap = Bitmap(); if (bitmap is null) return -1;
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
            return pixel[0] | pixel[1] << 8 | pixel[2] << 16;
        }
        async Task Until(Func<bool> condition, string name, int timeout = 18000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(25);
            Check(condition(), name + (errors.Count == 0 ? "" : " | " + string.Join("; ", errors)));
        }
        async Task Changing(string name)
        {
            int first = Pixel();
            await Until(() => Pixel() is 0x0000FF or 0x00FF00 && Pixel() != first, name);
        }
        var workerIds = new HashSet<int>();
        try
        {
            host.Show();
            await Until(() => WindowCaptureCatalog.List().Any(item => item.Options.ProcessId == source.Id),
                "Owned animated source is available to the real picker");
            var selected = WindowCaptureCatalog.List().Single(item => item.Options.ProcessId == source.Id).Options;
            var cue = new Cue { Kind = CueKind.WindowCapture, Capture = selected with { ControlAudio = true } };
            surface.ShowCue(cue);
            await Until(() => Bitmap() is not null && surface.IsPlaying, "Capture starts with real source pixels");
            workerIds.Add(Worker()!.Id);
            var captureAudio = (CapturedApplicationAudio.Lease)Field(surface, "_captureAudio")!;
            Check(captureAudio.Gain > .20 && captureAudio.Gain < .23, "Capture audio reaches the selected master gain");
            await Changing("Source pixels change before covering");
            cover.Show(); cover.WindowState = WindowState.Maximized;
            await Changing("Maximizing the application cover keeps the chosen source updating");
            cover.WindowState = WindowState.Normal;
            await Changing("Restoring the application cover keeps the chosen source updating");
            Check(errors.Count == 0, "Application maximize and restore do not report capture loss");

            RecoveryShowWindow((nint)selected.WindowHandle, 6); // Minimize only our fixture.
            // Cross the native no-frame deadline: recovery must still wait for
            // this exact source rather than requiring another picker selection.
            bool audioContinued = true;
            var outageDeadline = DateTime.UtcNow.AddSeconds(17);
            while (DateTime.UtcNow < outageDeadline)
            {
                audioContinued &= captureAudio.Gain > .20 && captureAudio.Gain < .23;
                await Task.Delay(50);
            }
            Check(audioContinued, "Established application audio continues through a long visual outage");
            int retained = Pixel();
            Check(Bitmap() is not null && retained is 0x0000FF or 0x00FF00,
                "Minimizing the source retains its last valid frame");
            RecoveryShowWindow((nint)selected.WindowHandle, 9);
            await Changing("Restored source automatically resumes without being reselected");
            workerIds.Add(Worker()!.Id);
            Check(!surface.IsPaused && errors.Count == 0, "A temporary unavailable source never leaves capture paused");

            var killedWorker = Worker()!; int killedId = killedWorker.Id;
            killedWorker.Kill(entireProcessTree: false);
            audioContinued = true;
            await Until(() =>
            {
                audioContinued &= captureAudio.Gain > .20 && captureAudio.Gain < .23;
                return Worker() is { } worker && worker.Id != killedId && Bitmap() is not null && surface.IsPlaying;
            },
                "A lost native capture worker is recreated automatically");
            Check(audioContinued, "Recreating the native image worker never interrupts established application audio");
            workerIds.Add(Worker()!.Id);
            await Changing("Recovered worker supplies live pixels without reselecting the cue");
            Check(errors.Count == 0 && !surface.IsPaused, "Recovered capture does not expose a stale failure");

            // Reconnect is pinned to the resolved HWND/PID/start time, never a title match.
            var reconnect = typeof(WindowCaptureCatalog).GetMethod("ResolveForReconnect", BindingFlags.Public | BindingFlags.Static);
            Check(reconnect is not null, "Reconnect exposes exact selected-window identity validation");
            bool rejected = false;
            try { reconnect!.Invoke(null, new object[] { selected with { WindowHandle = long.MaxValue } }); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Check(rejected, "A missing selected HWND cannot fall back to a same-title source");
            rejected = false;
            try { reconnect!.Invoke(null, new object[] { selected with { ProcessStartTimeUtcTicks = selected.ProcessStartTimeUtcTicks + 1 } }); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Check(rejected, "A reused process identity cannot reconnect to the selected window");

            await RunCaptureBackdropChecks(selected);

            RecoveryShowWindow((nint)selected.WindowHandle, 6);
            Worker()!.Kill(entireProcessTree: false);
            await Until(() => Worker() is null, "Unavailable source enters recovery without keeping a failed worker");
            surface.ShowCue(new Cue { Kind = CueKind.Text, Body = "Cancel pending recovery" });
            RecoveryShowWindow((nint)selected.WindowHandle, 9);
            await Task.Delay(1200);
            Check(Field(surface, "_captureClient") is null && Bitmap() is null && errors.Count == 0 && captureAudio.Gain == 0,
                "Replacing a recovering capture cancels retry and releases audio without a stale error or frame");
            surface.ShowCue(cue);
            await Until(() => Bitmap() is not null && surface.IsPlaying, "Capture can start again after recovery was cancelled");
            workerIds.Add(Worker()!.Id);
            captureAudio = (CapturedApplicationAudio.Lease)Field(surface, "_captureAudio")!;

            source.CloseMainWindow();
            await Until(() => errors.Count == 1 && surface.IsPaused, "A permanently closed source exhausts recovery with one visible error", 30000);
            Check(captureAudio.Gain == 0, "Terminal source loss silences its application audio lease");
            Check(Pixel() is 0x0000FF or 0x00FF00, "Final source loss retains the last valid image");
            surface.ShowCue(new Cue { Kind = CueKind.Text, Body = "After source loss" });
            await Task.Delay(1100);
            Check(Field(surface, "_captureClient") is null && Bitmap() is null && errors.Count == 1,
                "Changing cues cancels pending frames and prevents stale failures");
            foreach (int id in workerIds)
            {
                bool running;
                try { using var process = Process.GetProcessById(id); running = !process.HasExited; }
                catch (ArgumentException) { running = false; }
                Check(!running, "Retired capture worker " + id + " has exited");
            }
        }
        finally
        {
            surface.Dispose(); cover.Close(); host.Close(); CapturedApplicationAudio.ShutdownShared();
            try { if (!source.HasExited) { source.Kill(entireProcessTree: false); await source.WaitForExitAsync(); } }
            catch (InvalidOperationException) { }
        }
    }

    private static async Task RunCaptureBackdropChecks(WindowCaptureOptions selected)
    {
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 60,
            Transitions = new TransitionOptions { DurationMilliseconds = 220, AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 } };
        var host = new Window { Content = output, Title = "Culto capture test", Width = 640, Height = 360,
            Left = 800, Top = 420, ShowInTaskbar = false };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Field(ProjectionSurface surface, string name) => typeof(ProjectionSurface).GetField(name, flags)!.GetValue(surface);
        int Pixel(ProjectionSurface surface)
        {
            if (Field(surface, "_captureBitmap") is not BitmapSource bitmap) return -1;
            byte[] pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
            return pixel[0] | pixel[1] << 8 | pixel[2] << 16;
        }
        async Task Until(Func<bool> predicate, string name)
        {
            var deadline = DateTime.UtcNow.AddSeconds(18);
            while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(15);
            Check(predicate(), name);
        }
        var errors = new List<string>(); output.PlaybackError += errors.Add;
        var cue = new Cue { Kind = CueKind.WindowCapture, Capture = selected with { ControlAudio = true } };
        try
        {
            host.Show(); output.TransitionTo(cue);
            await Until(() => !output.IsTransitioning && output.IsPlaying && Pixel(output.ActiveSurface) >= 0,
                "Capture starts with live source pixels");
            var original = output.ActiveSurface;
            var client = Field(original, "_captureClient");
            var audio = (CapturedApplicationAudio.Lease)Field(original, "_captureAudio")!;
            await Until(() => audio.Gain > .20 && audio.Gain < .23, "Capture reaches its native audio gain");
            output.TransitionTo(new Cue { Kind = CueKind.WindowCapture, Capture = cue.Capture with { CropLeft = 20 } });
            await Until(() => !output.IsTransitioning && output.IsPlaying, "A capture crop edit becomes live");
            Check(!ReferenceEquals(output.ActiveSurface, original) && Field(output.ActiveSurface, "_captureClient") is { } changedClient
                && !ReferenceEquals(changedClient, client) && audio.Gain == 0,
                "Changing capture crop creates a new worker and releases the previous audio lease");
            var changedAudio = (CapturedApplicationAudio.Lease)Field(output.ActiveSurface, "_captureAudio")!;
            var visibleCapture = output.ActiveSurface;
            var visibleClient = Field(visibleCapture, "_captureClient");
            var visibleCue = output.DisplayedCue!;
            foreach (var kind in new[] { "black", "logo" })
            {
                output.TransitionToBackdrop(visibleCue, blackout: kind == "black", hideText: kind == "logo");
                await Until(() => !output.IsTransitioning, "Capture completes the " + kind + " backdrop normally");
                Check(output.IsPaused && changedAudio.Gain == 0 && ReferenceEquals(output.ActiveSurface, visibleCapture),
                    "Black or logo pauses the capture internally while hidden");
                output.TransitionToBackdrop(visibleCue, blackout: false, hideText: false);
                await Until(() => !output.IsTransitioning, "Capture restores from " + kind + " normally");
                Check(output.IsPlaying && !output.IsPaused && changedAudio.Gain > .20 && changedAudio.Gain < .23
                    && ReferenceEquals(output.ActiveSurface, visibleCapture) && ReferenceEquals(Field(visibleCapture, "_captureClient"), visibleClient),
                    "Restoring capture from " + kind + " automatically resumes the same worker and audio lease");
                int previous = Pixel(visibleCapture);
                await Until(() => Pixel(visibleCapture) != previous, "Restored capture pixels advance after " + kind);
            }
            output.ShowCue(visibleCue, blackout: true);
            Check(output.IsPaused && changedAudio.Gain == 0, "Immediate capture blackout pauses its internal source");
            output.ShowCue(visibleCue);
            Check(output.IsPlaying && !output.IsPaused && changedAudio.Gain > .20 && changedAudio.Gain < .23
                && ReferenceEquals(output.ActiveSurface, visibleCapture) && ReferenceEquals(Field(visibleCapture, "_captureClient"), visibleClient),
                "Immediate capture restore automatically resumes the same worker and audio lease");
            int restoredPixel = Pixel(visibleCapture);
            await Until(() => Pixel(visibleCapture) != restoredPixel, "Immediately restored capture pixels keep advancing");
            Check(errors.Count == 0, "Capture foreground changes produce no playback failures");
            output.Dispose();
            Check(audio.Gain == 0 && changedAudio.Gain == 0, "Disposing capture releases its application audio lease");
        }
        finally { output.Dispose(); host.Close(); }
    }

    [DllImport("user32.dll", EntryPoint = "ShowWindow")]
    private static extern bool RecoveryShowWindow(nint window, int command);
}
