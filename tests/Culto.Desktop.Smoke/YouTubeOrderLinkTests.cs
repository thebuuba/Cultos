using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static ClientWebSocket? _youTubePlaybackScript;
    private static void RunYouTubeOrderLinkTests(MainWindow window, string directory, bool native)
    {
        Test("Pasting YouTube links adds persistent order entries without navigating or projecting", () =>
        {
            var session = Field<PresentationSession>(window, "_session");
            var live = session.Live?.Id;
            var plan = Field<System.Collections.ObjectModel.ObservableCollection<Cue>>(window, "_plan");
            var count = plan.Count;
            var task = (Task<int>)Call(window, "AddYouTubeLinksToOrderAsync", "https://youtu.be/dQw4w9WgXcQ?t=42\nhttps://youtube.com/shorts/aqz-KE-bpKQ", false)!;
            Wait(task);
            Check(task.Result == 2 && plan.Count == count + 2, "Paste did not add both videos");
            Check(session.Live?.Id == live, "Pasting changed live output");
            var added = plan.Skip(count).ToArray();
            Check(added.All(cue => cue.Kind == CueKind.WindowCapture && cue.Capture is null && cue.YouTubeUrl is not null), "Order saved an ephemeral window instead of the video URL");
            Check(added[0].YouTubeUrl!.EndsWith("&t=42s"), "Paste lost start time");
            var loaded = new WorkspaceStore().Load().Plan.Items;
            Check(added.All(cue => loaded.Single(saved => saved.Id == cue.Id).YouTubeUrl == cue.YouTubeUrl), "Saved service lost YouTube links");
            var rejected = (Task<int>)Call(window, "AddYouTubeLinksToOrderAsync", "https://youtube.com.evil.test/watch?v=dQw4w9WgXcQ", false)!;
            Wait(rejected); Check(rejected.Result == 0 && plan.Count == count + 2, "Invalid URL changed the order");
        });
        Test("Pasted video title is visible immediately in the order and survives reload", () =>
        {
            var task = (Task<int>)Call(window, "AddYouTubeLinksToOrderAsync", "https://youtu.be/aqz-KE-bpKQ", true)!;
            Wait(task);
            var plan = Field<System.Collections.ObjectModel.ObservableCollection<Cue>>(window, "_plan");
            var cue = plan.Last();
            Check(!cue.Title.StartsWith("Video de YouTube"), "YouTube metadata did not resolve the title");
            var list = (ListBox)window.FindName("PlanList");
            list.ScrollIntoView(cue); list.UpdateLayout(); PumpFor(200);
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(cue);
            Check(row is not null && Descendants(row).OfType<TextBlock>().Any(text => text.Text == cue.Title),
                "Saved title is correct but the visible order still shows the video ID");
            Check(new WorkspaceStore().Load().Plan.Items.Single(item => item.Id == cue.Id).Title == cue.Title, "Reload lost the video title");
        });
        if (!native) return;
        var chrome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google/Chrome/Application/chrome.exe");
        if (!File.Exists(chrome)) { Console.WriteLine("SKIP real YouTube: Chrome is not installed"); return; }
        var browserWorkspace = directory;
        var verifyPlayback = Environment.GetCommandLineArgs().Contains("--native-youtube-autoplay");
        var initialPage = new Uri(verifyPlayback ? "https://www.youtube.com/watch?v=aqz-KE-bpKQ" : "https://www.youtube.com/");
        if (!verifyPlayback)
        {
            var page = Path.Combine(directory, "youtube-ready.html");
            File.WriteAllText(page, "<!doctype html><title>YouTube test window</title><p>Ready for navigation</p>");
            initialPage = new Uri(page);
        }
        var start = YouTubeProjectionBrowser.CreateStartInfo(chrome, Path.Combine(browserWorkspace, "YouTubeChrome"), initialPage);
        if (!verifyPlayback)
            foreach (var argument in start.ArgumentList.Where(value => value.StartsWith("--remote-debugging")).ToArray()) start.ArgumentList.Remove(argument);
        start.ArgumentList.Insert(0, "--mute-audio");
        using var ownedBrowser = Process.Start(start)!;
        try
        {
            Until(() => WindowCaptureCatalog.List().Any(target => target.Options.ProcessId == ownedBrowser.Id && YouTubeDiscovery.IsYouTube(target.Options)), "Test browser did not open YouTube");
            PumpFor(500);
            var original = WindowCaptureCatalog.List().Single(target => target.Options.ProcessId == ownedBrowser.Id && YouTubeDiscovery.IsYouTube(target.Options)).Options;
            if (verifyPlayback)
            {
                var readyDeadline = DateTime.UtcNow.AddSeconds(30);
                var playerReady = false;
                while (!playerReady && DateTime.UtcNow < readyDeadline)
                {
                    var ready = YouTubePlaybackProbeAsync(browserWorkspace, "String(document.readyState!=='loading'&&!!document.querySelector('.ytp-play-button'))");
                    Wait(ready); playerReady = ready.Result == "true";
                    if (!playerReady) PumpFor(200);
                }
                Check(playerReady, "Initial YouTube player did not finish loading");
                PumpFor(1500);
                // Model a userscript/autoplay policy that requires an explicit player click.
                // CDP is test instrumentation only; production still uses the existing window's accessibility controls.
                Wait(YouTubePlaybackProbeAsync(browserWorkspace,
                    "window.__cultoPlaybackGuard=true;document.addEventListener('play',e=>{if(e.target instanceof HTMLMediaElement&&!window.__cultoPlayClicked)e.target.pause();},true);document.addEventListener('click',e=>{if(e.isTrusted&&e.target.closest?.('.ytp-play-button,.ytp-large-play-button')){window.__cultoPlayClicked=true;window.__cultoPlayClicks=(window.__cultoPlayClicks||0)+1;}},true);", true));
            }
            Test("YouTube video links reuse the same real Chrome window and preserve its identity", () =>
            {
                try
                {
                var before = WindowCaptureCatalog.List().Where(target => target.Options.ProcessId == ownedBrowser.Id).Select(target => target.Options.WindowHandle).ToHashSet();
                var clipboard = System.Windows.Clipboard.GetText();
                ActivateYouTubeTestConsole(window);
                var firstTask = YouTubeProjectionBrowser.NavigateAsync(browserWorkspace, "https://www.youtube.com/watch?v=dQw4w9WgXcQ", default, original);
                Wait(firstTask); var first = firstTask.Result;
                Wait(YouTubeProjectionBrowser.EnsurePlaybackAsync(first));
                Check(first.ProcessId == ownedBrowser.Id, "Video resolved to another Chrome process");
                Check(first.WindowHandle == original.WindowHandle, "First video opened another window");
                Check(System.Windows.Clipboard.GetText() == clipboard, "Navigation replaced the user's clipboard");
                Check(WindowCaptureCatalog.List().Where(target => target.Options.ProcessId == ownedBrowser.Id).Select(target => target.Options.WindowHandle).ToHashSet().SetEquals(before), "Navigation opened another Chrome window");
                Until(() => WindowCaptureCatalog.List().Single(target => target.Options.WindowHandle == first.WindowHandle).Options.WindowTitle.Contains("Never Gonna Give You Up"), "Requested video did not load");
                if (verifyPlayback) CheckYouTubeIsPlaying(browserWorkspace, first);
                MinimizeYouTubeTestWindow((IntPtr)first.WindowHandle, 6);
                PumpFor(300);
                ActivateYouTubeTestConsole(window);
                var secondTask = YouTubeProjectionBrowser.NavigateAsync(browserWorkspace, "https://youtu.be/aqz-KE-bpKQ?t=2");
                Wait(secondTask); var second = secondTask.Result;
                Wait(YouTubeProjectionBrowser.EnsurePlaybackAsync(second));
                Check(first.WindowHandle == second.WindowHandle && first.ProcessId == second.ProcessId, "Second video opened another window");
                Until(() => WindowCaptureCatalog.List().Single(target => target.Options.WindowHandle == second.WindowHandle).Options.WindowTitle.Contains("Big Buck Bunny"), "Second navigation did not load the requested video");
                if (verifyPlayback) CheckYouTubeIsPlaying(browserWorkspace, second);
                Check(second.YouTubeOnly && !second.ControlAudio && WindowCaptureCatalog.WindowStillExists(second), "Link did not yield an available YouTube capture");
                var title = YouTubeVideoMetadata.ReadTitleAsync(new YouTubeLink("aqz-KE-bpKQ"));
                Wait(title); Check(!string.IsNullOrWhiteSpace(title.Result), "YouTube title was not retrieved");
                Console.WriteLine("YouTube title: " + title.Result);
                }
                catch
                {
                    Wait(DumpExistingYouTubeWindowAsync(original.WindowHandle));
                    if (verifyPlayback)
                    {
                        try
                        {
                        var probe = YouTubePlaybackProbeAsync(browserWorkspace, "JSON.stringify({guard:!!window.__cultoPlaybackGuard,clicked:!!window.__cultoPlayClicked,state:document.readyState,video:!!document.querySelector('video'),paused:document.querySelector('video')?.paused,controls:[...document.querySelectorAll('.ytp-play-button,.ytp-large-play-button')].map(e=>({name:e.getAttribute('aria-label'),display:getComputedStyle(e).display,visibility:getComputedStyle(e).visibility})),text:document.body.innerText.slice(0,500)})");
                        Wait(probe); Console.WriteLine("TEST PLAYBACK: " + probe.Result);
                        }
                        catch (Exception diagnosticError) { Console.WriteLine("TEST PROBE: " + diagnosticError.Message); }
                    }
                    throw;
                }
            });
            Test("Projecting an order link loads the existing YouTube window into Programa", () =>
            {
                if (verifyPlayback && !Environment.GetCommandLineArgs().Contains("--youtube-no-playback-delay"))
                    Wait(YouTubePlaybackProbeAsync(browserWorkspace, "addEventListener('DOMContentLoaded',()=>{const s=document.createElement('style');s.textContent='.ytp-play-button,.ytp-large-play-button{display:none!important}';document.head.append(s);setTimeout(()=>s.remove(),8000);},{once:true});", true));
                Call(window, "RefreshYouTube", (object?)null);
                var sources = (ListBox)window.FindName("YouTubeList");
                sources.SelectedItem = sources.Items.OfType<YouTubeSource>().Single(source => source.Cue.Capture!.WindowHandle == original.WindowHandle);
                var link = new Cue { Kind = CueKind.WindowCapture, Title = "YouTube desde el orden", YouTubeUrl = "https://www.youtube.com/watch?v=aqz-KE-bpKQ&t=2s" };
                var plan = Field<System.Collections.ObjectModel.ObservableCollection<Cue>>(window, "_plan");
                plan.Add(link);
                var order = (ListBox)window.FindName("PlanList");
                order.ScrollIntoView(link); order.UpdateLayout();
                var row = (ListBoxItem)order.ItemContainerGenerator.ContainerFromItem(link);
                ActivateYouTubeTestConsole(window);
                var projectionTime = Stopwatch.StartNew();
                order.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent, Source = row });
                var session = Field<PresentationSession>(window, "_session");
                var deadline = DateTime.UtcNow.AddSeconds(4);
                while ((session.Live?.Id != link.Id || session.Live.Capture is null) && DateTime.UtcNow < deadline) PumpFor(100);
                if (session.Live?.Id != link.Id || session.Live.Capture is null) Wait(DumpExistingYouTubeWindowAsync(original.WindowHandle));
                Check(session.Live?.Id == link.Id && session.Live.Capture?.ProcessId == ownedBrowser.Id,
                    "Order link waited for playback instead of projecting automatically: " + ((TextBlock)window.FindName("StatusText")).Text);
                Console.WriteLine("Automatic projection: " + projectionTime.ElapsedMilliseconds + " ms");
                var output = Field<ProjectionSurface>(window, "_output");
                deadline = DateTime.UtcNow.AddSeconds(25);
                while ((output.IsTransitioning || output.DisplayedCue?.Id != link.Id) && DateTime.UtcNow < deadline) PumpFor(100);
                Check(output.DisplayedCue?.Id == link.Id && !output.IsTransitioning, "YouTube capture did not reach Programa");
                Console.WriteLine("Visible capture: " + projectionTime.ElapsedMilliseconds + " ms");
                deadline = DateTime.UtcNow.AddSeconds(22);
                while (Field<CancellationTokenSource?>(window, "_youTubeLinkProjection") is not null && DateTime.UtcNow < deadline) PumpFor(100);
                Check(Field<CancellationTokenSource?>(window, "_youTubeLinkProjection") is null, "Automatic playback did not finish after projection");
                Console.WriteLine("Player confirmed: " + projectionTime.ElapsedMilliseconds + " ms");
                if (verifyPlayback) CheckYouTubeIsPlaying(browserWorkspace, session.Live!.Capture!);
                CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "youtube-order-programa.png"));
                var thumbnail = YouTubeVideoMetadata.ReadThumbnailAsync(link.YouTubeUrl, CancellationToken.None);
                Wait(thumbnail); Check(thumbnail.Result is { PixelWidth: > 0 }, "Order video thumbnail was not loaded");
                Test("Changing YouTube order entries reuses the running capture of the same Chrome window", () =>
                {
                    var reuseTime = Stopwatch.StartNew();
                    var capture = Field<object>(output, "_captureClient");
                    var next = session.Live!.Copy(newId: true);
                    next.Title = "Otro video en la misma ventana";
                    next.Capture = next.Capture! with { WindowTitle = next.Capture.WindowTitle + " updated title" };
                    Call(window, "PrepareCue", next, null);
                    Call(window, "ProjectPreparedCore", false, false, false);
                    var until = DateTime.UtcNow.AddSeconds(5);
                    while ((output.IsTransitioning || output.DisplayedCue?.Id != next.Id) && DateTime.UtcNow < until) PumpFor(50);
                    Check(output.DisplayedCue?.Id == next.Id, "Next order item did not reach output");
                    Check(ReferenceEquals(capture, Field<object>(output, "_captureClient")), "Same Chrome window unnecessarily restarted its capture worker");
                    Console.WriteLine("Visible next order entry: " + reuseTime.ElapsedMilliseconds + " ms");
                    var cropped = next.Copy(newId: true);
                    cropped.Capture = cropped.Capture! with { CropTop = cropped.Capture.CropTop + 5 };
                    Call(window, "PrepareCue", cropped, null);
                    Call(window, "ProjectPreparedCore", false, false, false);
                    until = DateTime.UtcNow.AddSeconds(5);
                    while ((output.IsTransitioning || output.DisplayedCue?.Id != cropped.Id) && DateTime.UtcNow < until) PumpFor(50);
                    Check(output.DisplayedCue?.Id == cropped.Id && !ReferenceEquals(capture, Field<object>(output, "_captureClient")),
                        "Changed capture settings reused an incompatible capture");
                });
            });
        }
        finally
        {
            _youTubePlaybackScript?.Dispose(); _youTubePlaybackScript = null;
            Call(window, "CloseOutput");
            if (!ownedBrowser.HasExited)
            {
                ownedBrowser.Refresh(); ownedBrowser.CloseMainWindow();
                if (!ownedBrowser.WaitForExit(2000)) { ownedBrowser.Kill(entireProcessTree: true); ownedBrowser.WaitForExit(3000); }
            }
            PumpFor(300);
        }
    }

    private static Task DumpExistingYouTubeWindowAsync(long handle) => Task.Run(() =>
    {
        var root = System.Windows.Automation.AutomationElement.FromHandle((nint)handle);
        Console.WriteLine("TEST WINDOW: " + root.Current.Name);
        var buttons = root.FindAll(System.Windows.Automation.TreeScope.Descendants,
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button));
        foreach (System.Windows.Automation.AutomationElement button in buttons)
            if (button.Current.ClassName.Contains("ytp-play-button") || button.Current.ClassName.Contains("ytp-large-play-button"))
                Console.WriteLine("TEST PLAYER BUTTON: " + button.Current.Name + " / " + button.Current.AutomationId + " / " + button.Current.ClassName);
        var edits = root.FindAll(System.Windows.Automation.TreeScope.Descendants,
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Edit));
        foreach (System.Windows.Automation.AutomationElement edit in edits)
            if (edit.TryGetCurrentPattern(System.Windows.Automation.ValuePattern.Pattern, out var pattern))
                Console.WriteLine("TEST ADDRESS: " + edit.Current.Name + " = " + ((System.Windows.Automation.ValuePattern)pattern).Current.Value);
    });

    private static void CheckYouTubeIsPlaying(string workspace, WindowCaptureOptions target)
    {
        var probe = YouTubePlaybackProbeAsync(workspace, "JSON.stringify({paused:document.querySelector('video')?.paused,time:document.querySelector('video')?.currentTime,guard:!!window.__cultoPlaybackGuard,clicked:!!window.__cultoPlayClicked,clicks:window.__cultoPlayClicks})");
        Wait(probe);
        using var state = JsonDocument.Parse(probe.Result);
        Check(state.RootElement.TryGetProperty("paused", out var paused) && !paused.GetBoolean()
            && state.RootElement.GetProperty("clicked").GetBoolean(), "Video stayed paused without an explicit player action: " + probe.Result);
        var initial = state.RootElement.GetProperty("time").GetDouble();
        PumpFor(1200);
        var later = YouTubePlaybackProbeAsync(workspace, "String(document.querySelector('video')?.currentTime)");
        Wait(later);
        Check(double.TryParse(later.Result, System.Globalization.CultureInfo.InvariantCulture, out var elapsed) && elapsed > initial,
            "Video time did not advance after starting playback");
        var ensure = YouTubeProjectionBrowser.EnsurePlaybackAsync(target);
        Wait(ensure);
        var unchanged = YouTubePlaybackProbeAsync(workspace, "JSON.stringify({paused:document.querySelector('video')?.paused,clicks:window.__cultoPlayClicks})");
        Wait(unchanged);
        using var after = JsonDocument.Parse(unchanged.Result);
        Check(!after.RootElement.GetProperty("paused").GetBoolean() && after.RootElement.GetProperty("clicks").GetInt32() == state.RootElement.GetProperty("clicks").GetInt32(),
            "An already playing video received an unnecessary play/pause click");
        Console.WriteLine("PASS YouTube actually plays after an explicit player action");
    }

    private static async Task<string> YouTubePlaybackProbeAsync(string workspace, string expression, bool install = false)
    {
        var port = (await File.ReadAllLinesAsync(Path.Combine(workspace, "YouTubeChrome/DevToolsActivePort")))[0];
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false });
        using var pages = JsonDocument.Parse(await client.GetStringAsync("http://127.0.0.1:" + port + "/json/list"));
        var page = pages.RootElement.EnumerateArray().First(item => item.GetProperty("type").GetString() == "page");
        using var probeSocket = install ? null : new ClientWebSocket();
        var socket = install ? (_youTubePlaybackScript ??= new ClientWebSocket()) : probeSocket!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        if (socket.State != WebSocketState.Open) await socket.ConnectAsync(new Uri(page.GetProperty("webSocketDebuggerUrl").GetString()!), timeout.Token);
        if (install) await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { id = 0, method = "Page.enable" }), WebSocketMessageType.Text, true, timeout.Token);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id = 1, method = install ? "Page.addScriptToEvaluateOnNewDocument" : "Runtime.evaluate",
            @params = install ? (object)new { source = expression } : new { expression, returnByValue = true } });
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[16384];
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            do { received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token); message.Write(buffer, 0, received.Count); } while (!received.EndOfMessage);
            using var response = JsonDocument.Parse(message.ToArray());
            if (!response.RootElement.TryGetProperty("id", out var id) || id.GetInt32() != 1) continue;
            if (response.RootElement.TryGetProperty("error", out var error)) throw new InvalidOperationException("Playback probe: " + error);
            return install ? "installed" : response.RootElement.GetProperty("result").GetProperty("result").GetProperty("value").GetString()!;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "ShowWindow")]
    private static extern bool MinimizeYouTubeTestWindow(IntPtr handle, int command);

    private static void ActivateYouTubeTestConsole(MainWindow window)
    {
        // Simulate the user's click on this test console before double-clicking an order entry.
        // Windows deliberately refuses focus transfers from an unattended background process.
        var previous = System.Windows.Forms.Cursor.Position;
        var point = window.PointToScreen(new System.Windows.Point(180, 14));
        System.Windows.Forms.Cursor.Position = new System.Drawing.Point((int)point.X, (int)point.Y);
        YouTubeTestMouseEvent(2, 0, 0, 0, UIntPtr.Zero);
        YouTubeTestMouseEvent(4, 0, 0, 0, UIntPtr.Zero);
        System.Windows.Forms.Cursor.Position = previous;
        PumpFor(150);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void YouTubeTestMouseEvent(uint flags, uint x, uint y, uint data, UIntPtr extra);
}
