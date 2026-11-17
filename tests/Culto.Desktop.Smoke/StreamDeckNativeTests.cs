using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;
using Culto.Remote;

internal static partial class Program
{
    private static void RunStreamDeckNativeTests(MainWindow window, string directory)
    {
        var bridgeType = typeof(MainWindow).Assembly.GetType("Culto.Desktop.Services.StreamDeckPluginBridge");
        Test("Native Stream Deck bridge exists independently of hotkeys", () => Check(bridgeType is not null, "Native Stream Deck bridge is missing"));
        if (bridgeType is null) return;
        var path = Path.Combine(directory, "streamdeck-connection.json");
        Test("Missing native plugin installer reports an actionable status", () =>
        {
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "StreamDeck", "com.cultostudio.control.streamDeckPlugin"))) return;
            Call(window, "InstallStreamDeckPluginClick", window, new System.Windows.RoutedEventArgs());
            Check(((TextBlock)window.FindName("StatusText")).Text.Contains("com.cultostudio.control.streamDeckPlugin"), "Missing installer did not explain how to obtain it");
        });
        Test("Native Stream Deck menu persists activation and releases its descriptor and listener", () =>
        {
            var menu = window.FindName("StreamDeckPluginEnabledMenu") as MenuItem;
            Check(menu is not null, "Native plugin menu is missing");
            Task Apply() => (Task)Call(window, "ApplyStreamDeckPluginSettingsAsync")!;
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var property = typeof(WorkspaceData).GetProperty("StreamDeckPluginEnabled")!;
            Check(!(bool)property.GetValue(workspace)!, "Native integration must default off");
            property.SetValue(workspace, true); Wait(Apply());
            Call(window, "SaveWorkspace");
            Check((bool)property.GetValue(new WorkspaceStore().Load())!, "Native integration setting did not persist");
            Check(!workspace.StreamDeckEnabled, "Native integration enabled global hotkeys");
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var descriptor = json.RootElement;
            var address = descriptor.GetProperty("address").GetString()!;
            var token = descriptor.GetProperty("token").GetString()!;
            Check(descriptor.GetProperty("schemaVersion").GetInt32() == 1 && descriptor.GetProperty("processId").GetInt32() == Environment.ProcessId,
                "Invalid descriptor contract");
            var uri = new Uri(address);
            Check(uri.Scheme == "http" && uri.Host == "127.0.0.1" && uri.Port > 0 && token.Length == 64, "Bridge did not publish loopback address and token");
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(8) };
            var unauthorized = http.GetAsync("/api/state"); Wait(unauthorized);
            Check(unauthorized.Result.StatusCode == HttpStatusCode.Unauthorized, "Native state was exposed without authentication");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var state = http.GetFromJsonAsync<RemoteSnapshot>("/api/state"); Wait(state);
            Check(state.Result is not null, "Authenticated native state was unavailable");
            var cue = new Cue { Kind = CueKind.Text, Title = "Native Stream Deck", Body = "Native command" };
            Field<System.Collections.ObjectModel.ObservableCollection<Cue>>(window, "_plan").Add(cue);
            Call(window, "PrepareCue", cue, null);
            using var foregroundWindow = new NativeStreamDeckFocusWindow();
            var foreground = PresenterGetForegroundWindow();
            var project = http.PostAsJsonAsync("/api/command", new RemoteCommand("project", "native-project")); Wait(project);
            var result = project.Result.Content.ReadFromJsonAsync<CommandResult>(); Wait(result);
            Check(result.Result?.Success == true && Field<PresentationSession>(window, "_session").Live?.Id == cue.Id, "Native project did not use existing output route");
            Check(PresenterGetForegroundWindow() == foreground, "Native command stole keyboard focus");
            var volume = (Slider)window.FindName("VolumeSlider"); volume.Value = 50;
            var command = new RemoteCommand("volume", "native-volume", Value: 45);
            var first = http.PostAsJsonAsync("/api/command", command); Wait(first);
            volume.Value = 60;
            var duplicate = http.PostAsJsonAsync("/api/command", command); Wait(duplicate);
            Check(volume.Value == 60, "Duplicate native command executed twice");
            volume.Value = 50;
            var upStep = new RemoteCommand("volume-step", "native-step-up", Value: 5);
            var steps = Task.WhenAll(http.PostAsJsonAsync("/api/command", upStep),
                http.PostAsJsonAsync("/api/command", new RemoteCommand("volume-step", "native-step-down", Value: -5)));
            Wait(steps);
            Check(steps.Result.All(response => response.IsSuccessStatusCode) && volume.Value == 50,
                "Native volume up/down did not apply to the latest slider value");
            var repeatedStep = http.PostAsJsonAsync("/api/command", upStep); Wait(repeatedStep);
            Check(volume.Value == 50, "Repeated native volume step executed twice");
            var maxStep = http.PostAsJsonAsync("/api/command", new RemoteCommand("volume-step", "native-step-max", Value: 100)); Wait(maxStep);
            Check(maxStep.Result.IsSuccessStatusCode && volume.Value == 100, "Native volume step exceeded upper limit");
            var minStep = http.PostAsJsonAsync("/api/command", new RemoteCommand("volume-step", "native-step-min", Value: -100)); Wait(minStep);
            Check(minStep.Result.IsSuccessStatusCode && volume.Value == 0, "Native volume step exceeded lower limit");
            var other = Activator.CreateInstance(bridgeType, directory, (Func<RemoteSnapshot>)(() => (RemoteSnapshot)Call(window, "BuildRemoteState")!),
                (Func<RemoteCommand, Task<CommandResult>>)(_ => Task.FromResult(new CommandResult(true, "test"))))!;
            try
            {
                var conflict = (Task)bridgeType.GetMethod("StartAsync")!.Invoke(other, null)!;
                try { Wait(conflict); throw new InvalidOperationException("Second owner replaced the live descriptor"); }
                catch (IOException) { }
                Check(JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("token").GetString() == token, "Conflicting owner overwrote descriptor");
            }
            finally { Wait(((IAsyncDisposable)other).DisposeAsync().AsTask()); }
            property.SetValue(workspace, false); Wait(Apply());
            Check(!File.Exists(path), "Disabling native integration left its credentials behind");
            var stopped = http.GetAsync("/api/state");
            try { Wait(stopped); throw new InvalidOperationException("Disabling left listener running"); }
            catch (HttpRequestException) { }
            property.SetValue(workspace, true); Wait(Apply());
            using var rotated = JsonDocument.Parse(File.ReadAllText(path));
            Check(rotated.RootElement.GetProperty("token").GetString() != token, "New native session reused credentials");
            Wait((Task)Call(window, "DisposeStreamDeckPluginAsync")!);
            Check(!File.Exists(path), "Closing native integration left descriptor behind");
            property.SetValue(workspace, false); Call(window, "SaveWorkspace");
        });
        Test("Native Stream Deck enable/disable races leave no listener descriptor", () =>
        {
            var workspace = Field<WorkspaceData>(window, "_workspace");
            typeof(WorkspaceData).GetProperty("StreamDeckPluginEnabled")!.SetValue(workspace, true);
            var enabling = (Task)Call(window, "ApplyStreamDeckPluginSettingsAsync")!;
            typeof(WorkspaceData).GetProperty("StreamDeckPluginEnabled")!.SetValue(workspace, false);
            var disabling = (Task)Call(window, "ApplyStreamDeckPluginSettingsAsync")!;
            Wait(Task.WhenAll(enabling, disabling));
            Check(!File.Exists(path), "Enable race republished disabled native credentials");
            var serviceDirectory = Path.Combine(directory, "native-race");
            var bridge = new StreamDeckPluginBridge(serviceDirectory, () => (RemoteSnapshot)Call(window, "BuildRemoteState")!,
                _ => Task.FromResult(new CommandResult(true, "test")));
            var starting = bridge.StartAsync();
            var closing = bridge.DisposeAsync().AsTask();
            Wait(Task.WhenAll(starting, closing));
            Check(!File.Exists(Path.Combine(serviceDirectory, "streamdeck-connection.json")), "Disposal during startup left credentials");
            try { Wait(bridge.StartAsync()); throw new InvalidOperationException("Disposed bridge restarted"); }
            catch (ObjectDisposedException) { }
        });
    }

    private static void RunStreamDeckNativeFixture(MainWindow window, string directory)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var plan = Field<System.Collections.ObjectModel.ObservableCollection<Cue>>(window, "_plan");
        plan.Clear();
        var first = new Cue { Kind = CueKind.Text, Title = "Stream Deck native fixture", Body = "Prepared native fixture" };
        var second = new Cue { Kind = CueKind.Text, Title = "Second native cue", Body = "Selected by stable ID" };
        plan.Add(first); plan.Add(second);
        Call(window, "PrepareCue", first, null);
        workspace.StreamDeckPluginEnabled = true;
        try
        {
            Wait((Task)Call(window, "ApplyStreamDeckPluginSettingsAsync")!);
            var descriptorPath = Path.Combine(directory, "streamdeck-connection.json");
            Check(File.Exists(descriptorPath), "Native bridge fixture failed to start");
            var fixture = Environment.GetEnvironmentVariable("CULTO_STREAMDECK_FIXTURE");
            if (!string.IsNullOrWhiteSpace(fixture)) File.WriteAllText(fixture, JsonSerializer.Serialize(new { descriptorPath, preparedCueId = first.Id }));
            Console.WriteLine("STREAMDECK_FIXTURE " + descriptorPath);
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline && (string.IsNullOrWhiteSpace(fixture) || !File.Exists(fixture + ".stop"))) PumpFor(100);
        }
        finally
        {
            workspace.StreamDeckPluginEnabled = false;
            Wait((Task)Call(window, "DisposeStreamDeckPluginAsync")!);
        }
    }

    private sealed class NativeStreamDeckFocusWindow : IDisposable
    {
        private readonly System.Windows.Window _window = new() { Title = "Native Stream Deck focus test", Width = 320, Height = 180, Topmost = true };
        public NativeStreamDeckFocusWindow() { _window.Show(); _window.Activate(); PumpFor(100); }
        public void Dispose() => _window.Close();
    }
}
