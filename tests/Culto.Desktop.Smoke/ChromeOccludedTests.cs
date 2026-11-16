using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunChromeOccludedTests(MainWindow window, bool baseline = false)
    {
        Test("Decoded Chrome video keeps rendering while an opaque application remains maximized above it", () =>
        {
            var folder = Path.GetFullPath(Path.Combine(".tools", "occluded-chrome-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            var page = Path.Combine(folder, "index.html");
            var video = Convert.ToBase64String(File.ReadAllBytes("tests/Culto.Renderer.Smoke/fixtures/chrome-occlusion.webm"));
            var title = "Culto owned occlusion YouTube " + Guid.NewGuid().ToString("N");
            File.WriteAllText(page, $"""
                <!doctype html><title>{title}</title><body style="margin:0;background:black">
                <video id="v" src="data:video/webm;base64,{video}" autoplay muted loop playsinline style="width:100vw;height:100vh;object-fit:fill"></video>
                <script>setInterval(()=>document.title='{title} '+v.readyState+' '+v.currentTime+(!v.paused&&v.currentTime>0&&v.readyState>=2?' playing':''),500)</script>
                """);
            var start = YouTubeProjectionBrowser.CreateStartInfo(@"C:\Program Files\Google\Chrome\Application\chrome.exe",
                Path.Combine(folder, "profile"), new Uri(Environment.GetEnvironmentVariable("CULTO_YOUTUBE_PROBE_URL") ?? page));
            var liveYouTube = Environment.GetEnvironmentVariable("CULTO_YOUTUBE_PROBE_URL") is not null;
            if (baseline)
                foreach (var argument in start.ArgumentList.Where(arg => arg.StartsWith("--disable-background")
                    || arg.StartsWith("--disable-renderer-backgrounding") || arg.Contains("CalculateNativeWinOcclusion")).ToArray())
                    start.ArgumentList.Remove(argument);
            foreach (var arg in new[] { "--autoplay-policy=no-user-gesture-required", "--window-position=30,30", "--window-size=640,480" })
                start.ArgumentList.Add(arg);
            using var chrome = Process.Start(start)!;
            var cover = new Window { Title = "Owned opaque other application", Background = Brushes.Magenta,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0, Width = 900, Height = 700, ShowInTaskbar = true };
            try
            {
                window.Topmost = false; window.WindowState = WindowState.Normal; window.Left = 750; window.Top = 40;
                bool IsProbe(WindowCaptureTarget t) => liveYouTube ? t.Options.ProcessId == chrome.Id : t.Options.WindowTitle.StartsWith(title);
                Until(() => WindowCaptureCatalog.List().Any(IsProbe), "Owned Chrome window did not open");
                var target = WindowCaptureCatalog.List().Single(IsProbe);
                ChromeProbeSetWindowPos((nint)target.Options.WindowHandle, (nint)(-1), 0, 0, 0, 0, 0x13);
                ChromeProbeSetForegroundWindow((nint)target.Options.WindowHandle);
                if (liveYouTube)
                {
                    Console.WriteLine("LIVE YOUTUBE: waiting for playback in owned Chrome, PID=" + chrome.Id);
                    PumpFor(15000);
                    target = WindowCaptureCatalog.List().Single(IsProbe);
                }
                else Until(() => WindowCaptureCatalog.List().Any(t => t.Options.WindowTitle.StartsWith(title) && t.Options.WindowTitle.EndsWith(" playing - Google Chrome")),
                    "Owned decoded WebM video did not start: " + string.Join(" | ", WindowCaptureCatalog.List().Where(IsProbe).Select(t => t.Options.WindowTitle)));
                SetField(window, "_projectionYouTubePreviousWindows", WindowCaptureCatalog.List()
                    .Where(item => item.Options.WindowHandle != target.Options.WindowHandle).Select(item => item.Options.WindowHandle).ToHashSet());
                SetField(window, "_projectionYouTubeDeadline", DateTime.UtcNow.AddSeconds(20));
                Call(window, "RefreshYouTube", (object?)null);
                Check(Field<PresentationSession>(window, "_session").Prepared?.Capture?.WindowHandle == target.Options.WindowHandle,
                    "New projection window was selected without being prepared for F5 or phone projection");
                Call(window, "PrepareCue", new Cue { Kind = CueKind.WindowCapture, Capture = target.Options with { YouTubeOnly = true, ControlAudio = false } }, null);
                Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                int Pixel()
                {
                    var bitmap = Field<BitmapSource?>(output, "_captureBitmap"); if (bitmap is null) return -1;
                    var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(640, 360, 1, 1), pixel, 4, 0);
                    return pixel[0] | pixel[1] << 8 | pixel[2] << 16;
                }
                Until(() => !output.IsTransitioning && Pixel() > 0, "Initial decoded video capture did not start");
                void SaveFrame(string stage)
                {
                    if (!liveYouTube || Field<BitmapSource?>(output, "_captureBitmap") is not { } bitmap) return;
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(".tools", "youtube-real-" + stage + ".png")); encoder.Save(stream);
                }
                SaveFrame("before-cover");
                var before = Pixel(); Until(() => Pixel() > 0 && Pixel() != before, "Decoded video is static before occlusion");
                var client = Field<object>(output, "_captureClient");
                var refreshCount = Field<int>(client, "_refreshCount");
                cover.Show(); cover.Topmost = true; cover.WindowState = WindowState.Maximized; cover.Activate();
                var coverHandle = new System.Windows.Interop.WindowInteropHelper(cover).Handle;
                Console.WriteLine($"COVER style={BrowserGetWindowLong(coverHandle, -16):X} exstyle={BrowserGetWindowLong(coverHandle, -20):X} region={BrowserGetWindowRgnBox(coverHandle, out _)}");
                Check((BrowserGetWindowLong(coverHandle, -16) & 0x80000000L) == 0
                    && (BrowserGetWindowLong(coverHandle, -20) & (0x20 | 0x80 | 0x80000)) == 0
                    && BrowserGetWindowRgnBox(coverHandle, out _) != 3,
                    "Test cover is excluded from Chromium's occlusion calculation");
                ChromeProbeSetWindowPos((nint)target.Options.WindowHandle, (nint)(-2), 0, 0, 0, 0, 0x13);
                PumpFor(1500);
                Check(!window.IsActive, "Other application did not keep focus");
                Until(() => Field<int>(client, "_refreshCount") > refreshCount,
                    "Capture was not renewed while the other application covered Culto Studio");
                Check(ReferenceEquals(client, Field<object>(output, "_captureClient")), "Cover recovery replaced the capture worker");
                PumpFor(1000);
                var settledRefreshCount = Field<int>(client, "_refreshCount");
                int changes = 0, last = Pixel();
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (DateTime.UtcNow < deadline)
                {
                    PumpFor(100); var next = Pixel();
                    if (next > 0 && next != last) { changes++; last = next; }
                    Check(!window.IsActive && cover.IsVisible && cover.WindowState == WindowState.Maximized,
                        "Capture activated the console or displaced the covering application");
                }
                Check(changes >= 6, "Source froze under the opaque maximized application: " + changes + " frame color changes in 10 seconds");
                Check(Field<int>(client, "_refreshCount") == settledRefreshCount,
                    "An unchanged covering window caused repeated capture resets");
                SaveFrame("covered");
                Console.WriteLine("OCCLUDED color changes=" + changes);
            }
            finally
            {
                if (liveYouTube) Console.WriteLine("LIVE WINDOWS: " + string.Join(" | ", WindowCaptureCatalog.List()
                    .Where(t => t.Options.Executable.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.Options.ProcessId + ": " + t.Options.WindowTitle)));
                cover.Close(); Call(window, "CloseOutput");
                if (!chrome.HasExited) chrome.Kill(entireProcessTree: true);
            }
        });
    }
}
