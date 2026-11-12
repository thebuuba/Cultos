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
    private static async Task RunLocalCaptureChecks()
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
            output.TransitionTo(new Cue { Kind = CueKind.WindowCapture, Capture = selected with { ControlAudio = false } });
            await Until(() => !output.IsTransitioning && output.IsPlaying && SourceIsColored(), "Off-tree output receives real source pixels");
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
