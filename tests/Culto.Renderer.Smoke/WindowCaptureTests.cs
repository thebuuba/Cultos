using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    // Explicit native/GPU suite. Creates only its own synthetic source/occluder.
    // Publish MoveHost to artifacts/CaptureHost.Tests before running this selector.
    private static async Task RunWindowCaptureChecks()
    {
        const int width = 320, height = 180;
        string worker = Path.GetFullPath("artifacts/CaptureHost.Tests/Culto.MoveHost.exe");
        string runtime = Path.GetFullPath("artifacts/ObsMoveRuntime");
        Check(File.Exists(worker) && File.Exists(Path.Combine(runtime, "bin/64bit/libobs-winrt.dll")),
            "Native capture worker and pinned OBS WinRT runtime are available");
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        var middle = new Border { Background = Brushes.Blue };
        var left = new Border { Background = Brushes.Red };
        var right = new Border { Background = Brushes.Red };
        Grid.SetColumn(middle, 1); Grid.SetColumn(right, 2);
        content.Children.Add(left); content.Children.Add(middle); content.Children.Add(right);
        var source = new Window { Title = "Culto capture synthetic source", Width = 520, Height = 330,
            Left = 60, Top = 60, Content = content, ShowInTaskbar = false };
        var occluder = new Window { Title = "Culto capture synthetic occluder", Width = 600, Height = 410,
            Left = 30, Top = 30, Background = Brushes.Magenta, Topmost = true, ShowInTaskbar = false };
        var animation = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        bool blue = true;
        animation.Tick += (_, _) => { blue = !blue; middle.Background = blue ? Brushes.Blue : Brushes.Lime; };
        var gate = new object();
        var frames = new List<byte[]>();
        void OnFrame(byte[] pixels) { lock (gate) { frames.Add(pixels); if (frames.Count > 120) frames.RemoveAt(0); } }
        byte[][] Snapshot() { lock (gate) return frames.ToArray(); }
        async Task Until(Func<bool> condition, string name, Task? captureTask = null, int timeout = 18000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                if (captureTask is { IsCompleted: true }) await captureTask;
                await Task.Delay(25);
            }
            Check(condition(), name);
        }
        bool Blue(byte[] pixels, int x, int y) { int i = (y * width + x) * 4; return pixels[i] > 180 && pixels[i + 1] < 60 && pixels[i + 2] < 60; }
        bool Green(byte[] pixels, int x, int y) { int i = (y * width + x) * 4; return pixels[i + 1] > 180 && pixels[i] < 60 && pixels[i + 2] < 60; }
        bool Red(byte[] pixels, int x, int y) { int i = (y * width + x) * 4; return pixels[i + 2] > 180 && pixels[i] < 60 && pixels[i + 1] < 60; }
        bool Black(byte[] pixels, int x, int y) { int i = (y * width + x) * 4; return pixels[i] < 12 && pixels[i + 1] < 12 && pixels[i + 2] < 12; }
        try
        {
            source.Show(); source.Activate(); animation.Start();
            await Task.Delay(200);
            var hwnd = new WindowInteropHelper(source).Handle;
            var className = new StringBuilder(1024); CaptureTestGetClassName(hwnd, className, className.Capacity);
            using var ownProcess = Process.GetCurrentProcess();
            var options = new WindowCaptureOptions { WindowHandle = hwnd.ToInt64(), ProcessId = ownProcess.Id,
                ProcessStartTimeUtcTicks = ownProcess.StartTime.ToUniversalTime().Ticks, WindowClass = className.ToString(),
                WindowTitle = source.Title, Executable = ownProcess.MainModule!.FileName, ControlAudio = false };
            using (var cancellation = new CancellationTokenSource())
            using (var client = new ObsWindowCaptureClient(worker, runtime))
            {
                var capture = client.CaptureAsync(options, width, height, OnFrame, cancellation.Token);
                await Until(() => Snapshot().Any(f => Blue(f, 160, 90)) && Snapshot().Any(f => Green(f, 160, 90)),
                    "Exact-window capture delivers changing source pixels", capture);
                Check(Snapshot().All(f => f.Length == width * height * 4), "Captured frames match the exact BGRA contract");
                occluder.Show();
                lock (gate) frames.Clear();
                await Until(() => Snapshot().Length >= 10 && Snapshot().Any(f => Blue(f, 160, 90))
                    && Snapshot().Any(f => Green(f, 160, 90)), "Capture keeps updating while another window covers the source", capture);
                Check(Snapshot().All(f => Blue(f, 160, 90) || Green(f, 160, 90)),
                    "Occluding window pixels never replace the selected source");
                cancellation.Cancel();
                try { await capture.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { }
                Check(capture.IsCompleted, "Cancelling capture promptly stops its worker stream");
            }
            lock (gate) frames.Clear();
            using (var cancellation = new CancellationTokenSource())
            using (var client = new ObsWindowCaptureClient(worker, runtime))
            {
                var placed = options with { Transform = new(.5, .1, .5, .8) };
                var capture = client.CaptureAsync(placed, width, height, OnFrame, cancellation.Token);
                await Until(() => Snapshot().Length >= 8, "Positioned and stretched capture produces frames", capture);
                Check(Snapshot().All(f => Black(f, 80, 90) && Black(f, 240, 5) && Black(f, 240, 175)),
                    "Native transform keeps black outside its placed output rectangle");
                Check(Snapshot().All(f => Blue(f, 240, 90) || Green(f, 240, 90)),
                    "Native transform places and stretches the chosen window instead of recentering it");
                cancellation.Cancel();
                try { await capture.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { }
            }
            lock (gate) frames.Clear();
            var dpi = VisualTreeHelper.GetDpi(source);
            options = options with { CropLeft = (int)Math.Round(80 * dpi.DpiScaleX), CropRight = (int)Math.Round(80 * dpi.DpiScaleX) };
            using (var cancellation = new CancellationTokenSource())
            using (var client = new ObsWindowCaptureClient(worker, runtime))
            {
                var capture = client.CaptureAsync(options, width, height, OnFrame, cancellation.Token);
                await Until(() => Snapshot().Length >= 8, "Cropped window capture produces real frames", capture);
                Check(Snapshot().All(f => !Red(f, 160, 90) && (Blue(f, 160, 90) || Green(f, 160, 90))),
                    "Crop keeps the intended center region");
                Check(Snapshot().All(f => Black(f, 2, 90) && Black(f, 317, 90)),
                    "Cropped capture preserves its aspect ratio with black side margins");
                Check(Snapshot().All(f => !Red(f, 75, 90) && !Red(f, 245, 90)),
                    "Cropped side stripes do not bleed into the output");
                animation.Stop(); source.Close();
                Exception? closed = null;
                try { await capture.WaitAsync(TimeSpan.FromSeconds(6)); }
                catch (Exception error) { closed = error; }
                Check(closed is InvalidOperationException && closed.Message.Contains("ventana", StringComparison.OrdinalIgnoreCase),
                    "Closing the source reports loss instead of capturing another window");
            }
            await Task.Delay(250);
            var workers = Process.GetProcessesByName("Culto.MoveHost");
            try
            {
                Check(workers.All(p => { try { return !string.Equals(p.MainModule?.FileName, worker, StringComparison.OrdinalIgnoreCase); } catch { return true; } }),
                    "Capture cancellation and source loss leave no test worker running");
            }
            finally { foreach (var process in workers) process.Dispose(); }
        }
        finally { animation.Stop(); occluder.Close(); if (source.IsVisible) source.Close(); }
    }

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int CaptureTestGetClassName(nint window, StringBuilder buffer, int length);
}
