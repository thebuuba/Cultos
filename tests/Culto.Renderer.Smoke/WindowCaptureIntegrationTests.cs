using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    // Program.Main routes --capture-source here BEFORE creating its usual App.
    // This child is intentionally a different PID: the picker excludes Culto's
    // own process to prevent capturing the projection recursively.
    private static void RunCaptureSource()
    {
        Directory.CreateDirectory(Folder);
        var audioPath = Path.Combine(Folder, "isolation-source-440.wav");
        WriteIsolationTone(audioPath, 440);
        var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        using var tone = new ProjectionSurface { Volume = 60, Loop = true,
            Transitions = new TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 } };
        var panel = new Border { Background = Brushes.Blue };
        var source = new Window { Title = Environment.GetEnvironmentVariable("CULTO_CAPTURE_SOURCE_TITLE") ?? "Culto capture integration source " + Environment.ProcessId,
            Left = 40, Top = 40, Width = 520, Height = 330, Content = panel };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        bool blue = true;
        timer.Tick += (_, _) => { blue = !blue; panel.Background = blue ? Brushes.Blue : Brushes.Lime; };
        application.Startup += (_, _) =>
        {
            source.Show();
            if (Environment.GetEnvironmentVariable("CULTO_CAPTURE_SOURCE_SILENT") != "1")
                tone.ShowCue(new Cue { Kind = CueKind.Audio, FilePath = audioPath });
            timer.Start();
        };
        application.Exit += (_, _) => { timer.Stop(); tone.Dispose(); };
        application.Run();
    }

    private static async Task RunWindowCaptureIntegrationChecks()
    {
        Check(ObsWindowCaptureClient.IsAvailable, "Integration uses the shipped capture worker and runtime layout");
        var sourceStart = new ProcessStartInfo(Path.GetFullPath(".tools/dotnet/dotnet.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Environment.CurrentDirectory
        };
        sourceStart.ArgumentList.Add(typeof(Program).Assembly.Location);
        sourceStart.ArgumentList.Add("--capture-source");
        using var sourceProcess = Process.Start(sourceStart) ?? throw new InvalidOperationException("Cannot start the owned capture fixture.");
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 60 };
        var mirror = new Rectangle { Fill = new VisualBrush(output) { Stretch = Stretch.Uniform } };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(mirror, 1); grid.Children.Add(output); grid.Children.Add(mirror);
        var host = new Window { Title = "Culto capture integration output", Content = grid,
            Left = 550, Top = 70, Width = 900, Height = 330, Topmost = true };
        var errors = new List<string>(); output.PlaybackError += errors.Add;
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        CapturedApplicationAudio.Lease? Audio(ProjectionSurface surface) => (CapturedApplicationAudio.Lease?)typeof(ProjectionSurface)
            .GetField("_captureAudio", fields)!.GetValue(surface);
        BitmapSource? Image(ProjectionSurface surface) => (BitmapSource?)typeof(ProjectionSurface)
            .GetField("_captureBitmap", fields)!.GetValue(surface);
        int Pixel(BitmapSource bitmap)
        {
            byte[] pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
            return pixel[0] | pixel[1] << 8 | pixel[2] << 16;
        }
        bool MirrorsMatch()
        {
            // Render the common parent once. Rendering an arranged child alone
            // retains its layout offset, which can put it outside that bitmap.
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)grid.ActualWidth), Math.Max(1, (int)grid.ActualHeight),
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(grid);
            byte[] first = new byte[4], second = new byte[4];
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 4, bitmap.PixelHeight / 2, 1, 1), first, 4, 0);
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth * 3 / 4, bitmap.PixelHeight / 2, 1, 1), second, 4, 0);
            bool sourceColor = first[2] < 60 && (first[0] > 180 && first[1] < 60 || first[1] > 180 && first[0] < 60);
            return sourceColor && first.SequenceEqual(second);
        }
        async Task Until(Func<bool> condition, string name, int milliseconds = 18000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(20);
            Check(condition(), name + (errors.Count == 0 ? "" : " | " + string.Join("; ", errors)));
        }
        try
        {
            host.Show(); host.Activate();
            await Until(() => WindowCaptureCatalog.List().Any(item => item.Options.ProcessId == sourceProcess.Id),
                "The real catalog discovers the separate synthetic source process");
            var selected = WindowCaptureCatalog.List().Single(item => item.Options.ProcessId == sourceProcess.Id).Options;
            selected = WindowCaptureCatalog.Resolve(selected);
            var cue = new Cue { Kind = CueKind.WindowCapture, Title = "Captured source", Capture = selected with { ControlAudio = true } };
            using (var prepared = new ProjectionSurface(preview: true))
            {
                var preparationErrors = new List<string>(); prepared.PlaybackError += preparationErrors.Add;
                prepared.ShowCue(new Cue { Kind = CueKind.WindowCapture, Capture = selected with
                {
                    WindowHandle = 0, ProcessId = -1, WindowTitle = "missing synthetic source " + Guid.NewGuid().ToString("N")
                } });
                Check(preparationErrors.Count == 1, "Unavailable prepared capture reports a recoverable error");
                prepared.ShowCue(new Cue { Kind = CueKind.WindowCapture, Capture = selected with { ControlAudio = false } });
                await Until(() => Image(prepared) is not null && !prepared.IsPaused,
                    "A valid capture can be prepared after an earlier preparation failure");
                int firstPreparedFrame = Pixel(Image(prepared)!);
                await Until(() => Pixel(Image(prepared)!) != firstPreparedFrame,
                    "Retried capture preview keeps updating instead of remaining paused");
                Check(Audio(prepared) is null, "Preparing a capture does not acquire or duplicate its application audio");
            }
            using var recording = new OutputAudioCapture("audio-capture-integration-loopback.wav");
            await Task.Delay(900);
            var referenceStart = recording.Count;
            await Task.Delay(600);
            var reference = MeasureAudioSpectrum(recording.Snapshot(referenceStart)).Tone440;
            Check(reference > .00001, "Separate source is audible before capture controls its session");
            output.ShowCue(new Cue { Kind = CueKind.Text, Body = "Before capture" });
            output.TransitionTo(cue); output.SetPaused(false);
            await Until(() => output.DisplayedCue?.Id == cue.Id && !output.IsTransitioning && output.IsPlaying
                && Image(output.ActiveSurface) is not null, "Captured window becomes a live mixer source");
            var active = output.ActiveSurface;
            var lease = Audio(active);
            await Until(() => lease?.HasSessions == true && lease.Gain > .20 && lease.Gain < .23,
                "Capture audio is bound to the source process at the same master gain as video");
            var liveStart = recording.Count;
            await Task.Delay(500);
            var captured = MeasureAudioSpectrum(recording.Snapshot(liveStart)).Tone440;
            Check(captured / reference is > .14 and < .30,
                $"Captured source audio has one attenuated copy, without echo or duplicate output (ratio {captured / reference:0.000})");
            Check(MirrorsMatch(), "Local output and preview mirror display the same captured frame");
            output.SetPaused(true);
            int frozen = Pixel(Image(active)!);
            await Task.Delay(550);
            Check(output.IsPaused && Pixel(Image(active)!) == frozen && lease!.Gain == 0,
                "Pausing freezes the captured image and silences only its source audio");
            output.SetPaused(false);
            await Until(() => !output.IsPaused && Pixel(Image(active)!) != frozen && lease!.Gain > .20,
                "Resuming capture updates its image and restores its audio gain");
            output.TransitionToBackdrop(cue, blackout: true, hideText: false);
            await Until(() => !output.IsTransitioning, "Capture fades to black");
            Check(output.IsPaused && lease!.Gain == 0 && ReferenceEquals(output.ActiveSurface, active),
                "Black retains the capture source paused and silent");
            output.TransitionToBackdrop(cue, blackout: false, hideText: false);
            await Until(() => !output.IsTransitioning, "Captured content restores from black");
            Check(!output.IsPaused && ReferenceEquals(output.ActiveSurface, active),
                "Restored capture resumes automatically with the same live source");
            await Until(() => output.IsPlaying && lease!.Gain > .20, "Automatic restore returns captured audio after black");

            var video = new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv"), Title = "Video after capture" };
            output.Loop = true;
            output.TransitionTo(video); output.SetPaused(false);
            await Until(() => output.DisplayedCue?.Id == video.Id && !output.IsTransitioning && output.IsPlaying,
                "A normal video can replace a captured window through the mixer");
            Check(Audio(active) is null && !lease!.HasSessions && lease.Gain == 0,
                "Retiring the captured surface releases its audio lease at zero gain");
            output.SetPaused(true);
            await Task.Delay(200);
            var silenceStart = recording.Count;
            await Task.Delay(450);
            Check(MeasureAudioSpectrum(recording.Snapshot(silenceStart)).Tone440 < reference * .04,
                "Off-screen source stays silent after its capture is replaced");

            output.TransitionTo(cue); output.SetPaused(false);
            await Until(() => output.DisplayedCue?.Id == cue.Id && !output.IsTransitioning && output.IsPlaying,
                "Selecting capture again creates a live source with audio control");
            sourceProcess.CloseMainWindow();
            await Until(() => errors.Count > 0 && output.IsPaused, "Closing the captured application reports loss and silences the capture");
            output.TransitionTo(video); output.SetPaused(false);
            await Until(() => output.DisplayedCue?.Id == video.Id && !output.IsTransitioning && output.IsPlaying,
                "Video output recovers after capture source loss");
            int previousErrors = errors.Count;
            output.TransitionTo(cue);
            await Until(() => errors.Count == previousErrors + 1 && !output.IsTransitioning,
                "An unavailable incoming capture reports an error");
            Check(output.DisplayedCue?.Id == video.Id && output.IsPlaying,
                "An unavailable incoming capture preserves the currently playing video");
            await recording.StopAsync();
        }
        finally
        {
            output.Dispose(); host.Close(); CapturedApplicationAudio.ShutdownShared();
            try
            {
                if (!sourceProcess.HasExited)
                {
                    sourceProcess.CloseMainWindow();
                    try { await sourceProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4)); }
                    catch (TimeoutException) { sourceProcess.Kill(entireProcessTree: false); }
                }
            }
            catch (InvalidOperationException) { }
        }
    }
}
