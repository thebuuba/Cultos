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
using Culto.Core;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static async Task RunLocalCaptureChecks(bool switchToFile = false)
    {
        Check(ObsWindowCaptureClient.IsAvailable, "Local capture has the shipped native capture worker");
        var start = new ProcessStartInfo(Path.GetFullPath(".tools/dotnet/dotnet.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Environment.CurrentDirectory
        };
        start.Environment["CULTO_CAPTURE_SOURCE_SILENT"] = "1";
        start.ArgumentList.Add(typeof(Program).Assembly.Location); start.ArgumentList.Add("--capture-source");
        using var source = Process.Start(start) ?? throw new Exception("Cannot start the owned source.");
        using var output = new ProjectionSurface(smoothTransitions: true) { Width = 1920, Height = 1080, Volume = 0 };
        using var first = new OutputMonitor(); using var second = new OutputMonitor();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(first); grid.Children.Add(second); Grid.SetColumn(second, 1);
        var host = new Window { Title = "Culto local capture resize test", Content = grid, Width = 850, Height = 330,
            Left = 600, Top = 50, Topmost = true, ShowInTaskbar = false };
        var errors = new List<string>(); output.PlaybackError += errors.Add;
        long engineMilliseconds = -1;
        var mixer = typeof(ProjectionSurface).GetField("_mixer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(output)!;
        if (switchToFile)
            mixer.GetType().GetField("_engineFactory", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mixer,
                (Func<LibVLCSharp.Shared.LibVLC>)(() =>
                {
                    var engineClock = Stopwatch.StartNew();
                    var engine = ProjectionSurface.CreatePlaybackEngine();
                    System.Threading.Interlocked.Exchange(ref engineMilliseconds, engineClock.ElapsedMilliseconds);
                    return engine;
                }));
        object? Field(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
        bool SourceIsColored()
        {
            if (Field(output.ActiveSurface, "_captureBitmap") is not BitmapSource image) return false;
            var pixel = new byte[4];
            image.CopyPixels(new Int32Rect(image.PixelWidth / 2, image.PixelHeight / 2, 1, 1), pixel, 4, 0);
            return pixel[2] < 40 && (pixel[0] > 180 || pixel[1] > 180);
        }
        int MonitorColor(OutputMonitor monitor)
        {
            var image = new RenderTargetBitmap(Math.Max(1, (int)grid.ActualWidth), Math.Max(1, (int)grid.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            image.Render(grid);
            var pixel = new byte[4];
            var center = monitor.TranslatePoint(new Point(monitor.ActualWidth / 2, monitor.ActualHeight / 2), grid);
            image.CopyPixels(new Int32Rect((int)center.X, (int)center.Y, 1, 1), pixel, 4, 0);
            return pixel[0] | pixel[1] << 8 | pixel[2] << 16;
        }
        async Task Until(Func<bool> condition, string name, int milliseconds = 18000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(30);
            Check(condition(), name + (errors.Count == 0 ? "" : " | " + string.Join("; ", errors)));
        }
        async Task CheckMonitors(string state)
        {
            foreach (var monitor in new[] { first, second })
            {
                await Until(() => MonitorColor(monitor) is 0x0000FF or 0x00FF00, "Disconnected capture is visible " + state);
                var previous = MonitorColor(monitor);
                await Until(() => MonitorColor(monitor) is 0x0000FF or 0x00FF00 && MonitorColor(monitor) != previous,
                    "Disconnected capture keeps changing " + state);
            }
        }
        try
        {
            host.Show();
            output.Measure(new Size(1920, 1080)); output.Arrange(new Rect(0, 0, 1920, 1080)); output.UpdateLayout();
            first.SetLocalSource(output); second.SetLocalSource(output);
            await Until(() => WindowCaptureCatalog.List().Any(item => item.Options.ProcessId == source.Id), "Owned capture source is available");
            var selected = WindowCaptureCatalog.List().Single(item => item.Options.ProcessId == source.Id).Options;
            if (!switchToFile)
            {
                var thumbnail = new Culto.Desktop.WindowCaptureThumbnail { Width = 160, Height = 90,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
                    Cue = new Cue { Kind = CueKind.WindowCapture, Capture = selected with { ControlAudio = false } } };
                grid.Children.Add(thumbnail);
                await Until(() => thumbnail.Content is ProjectionSurface surface && Field(surface, "_captureBitmap") is BitmapSource,
                    "The actual YouTube thumbnail receives captured pixels");
                var bitmap = (BitmapSource)Field((ProjectionSurface)thumbnail.Content, "_captureBitmap")!;
                Check(bitmap.PixelWidth == 320 && bitmap.PixelHeight == 180,
                    "YouTube thumbnail captures at 320x180 instead of duplicating the Full HD program stream");
                grid.Children.Remove(thumbnail);
            }
            output.TransitionTo(new Cue { Kind = CueKind.WindowCapture, Capture = selected with { ControlAudio = false } });
            await Until(() => !output.IsTransitioning && output.IsPlaying && SourceIsColored(), "Off-tree output receives real source pixels");
            if (!switchToFile)
            {
                var image = (BitmapSource)Field(output.ActiveSurface, "_captureBitmap")!;
                int updates = 0;
                EventHandler changed = (_, _) => updates++;
                image.Changed += changed;
                var clock = Stopwatch.StartNew();
                await Task.Delay(2200);
                double fps = updates / clock.Elapsed.TotalSeconds;
                image.Changed -= changed;
                Check(image.PixelWidth == 1920 && image.PixelHeight == 1080,
                    "Program captures at 1920x1080 while the thumbnail stays small");
                Check(fps is >= 24 and <= 40,
                    $"WPF program image uses the lighter 30 fps capture ({fps:0.0} updates/s)");
            }
            if (switchToFile)
            {
                var capture = output.DisplayedCue!;
                var video = new Cue { Kind = CueKind.Video, Title = "File replacing capture", FilePath = Path.Combine(Folder, "test.mkv") };
                await Until(() => !output.IsPreparingPlaybackEngine, "Capture can finish warming the local video engine", 30000);
                var engineWasReadyBeforeFirstFile = engineMilliseconds >= 0;
                var requested = Stopwatch.StartNew();
                output.TransitionTo(video); output.SetPaused(false);
                await Until(() => output.DisplayedCue?.Id == video.Id && !output.IsTransitioning && output.IsPlaying,
                    "One request replaces a live capture with the first local video", 30000);
                Results.Add($"INFO first capture-to-file request: {requested.ElapsedMilliseconds} ms; native engine: {engineMilliseconds} ms");
                for (var i = 0; i < 3; i++)
                {
                    output.TransitionTo(capture); output.SetPaused(false);
                    await Until(() => output.DisplayedCue?.Id == capture.Id && !output.IsTransitioning && SourceIsColored(), "Capture returns for the next file change");
                    output.TransitionTo(video); output.SetPaused(false);
                    var pending = Field(mixer, "_incoming");
                    Check(pending is not null, "The local file is preparing its first decoded frame");
                    output.TransitionTo(video); output.SetPaused(false);
                    Check(ReferenceEquals(pending, Field(mixer, "_incoming")), "Repeating the same pending file request keeps its decoder instead of starting over");
                    await Until(() => output.DisplayedCue?.Id == video.Id && !output.IsTransitioning && output.IsPlaying,
                        "One request replaces capture with an already initialized video player");
                }
                Check(engineWasReadyBeforeFirstFile, "A live capture prepares the local video engine before the first file request");
                Check(errors.Count == 0, "Capture-to-file changes report no playback failures");
                return;
            }
            await CheckMonitors("before maximizing");
            var client = Field(output.ActiveSurface, "_captureClient");
            for (var i = 0; i < 3; i++)
            {
                host.WindowState = WindowState.Maximized; await Task.Delay(100);
                await CheckMonitors("while maximized");
                host.WindowState = WindowState.Normal; await Task.Delay(100);
                await CheckMonitors("after restoring");
            }
            Check(ReferenceEquals(client, Field(output.ActiveSurface, "_captureClient")), "Resize never recreates the source capture");
            Check(errors.Count == 0, "Local capture resize produces no playback errors");
        }
        finally
        {
            first.SetLocalSource(null); second.SetLocalSource(null); host.Close();
            try { if (!source.HasExited) source.Kill(); } catch (InvalidOperationException) { }
        }
    }
}
