using System.Collections;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Interop;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunStreamDeckTests(MainWindow window)
    {
        var type = typeof(MainWindow).Assembly.GetType("Culto.Desktop.Services.StreamDeckHotkeys");
        Test("Stream Deck exposes a Windows global hotkey bridge", () => Check(type is not null, "Stream Deck hotkey bridge is missing"));
        if (type is null) return;
        var handle = new WindowInteropHelper(window).Handle;
        const int firstId = 0x4360, reserved = 0x4390;
        var actions = new List<string>();
        using var bridge = (IDisposable)Activator.CreateInstance(type, handle, (Action<string>)actions.Add)!;
        bool Enable(out string? error)
        {
            object?[] parameters = [null];
            var result = (bool)type.GetMethod("TryEnable")!.Invoke(bridge, parameters)!;
            error = (string?)parameters[0]; return result;
        }
        void Disable() => type.GetMethod("Disable")!.Invoke(bridge, null);
        try
        {
            Test("Stream Deck dispatches all dedicated keys once without stealing focus", () =>
            {
                Check(Enable(out var error), error ?? "Could not register Stream Deck keys");
                var foreground = PresenterGetForegroundWindow();
                for (var index = 0; index < 12; index++) PresenterSendMessage(handle, 0x0312, firstId + index, (0x7c + index) << 16);
                Check(actions.SequenceEqual(new[] { "project", "previous", "next", "background", "blackout", "pause", "restart", "volume-down", "volume-up", "return-media", "scene-youtube", "scene-image" }), "Incorrect F13–F24 action mapping");
                PresenterSendMessage(handle, 0x0312, firstId, 0x7d << 16);
                PresenterSendMessage(handle, 0x0312, firstId, (0x7c << 16) | 2);
                Check(actions.Count == 12, "Stale key or modified shortcut triggered an action");
                Check(PresenterGetForegroundWindow() == foreground, "Stream Deck bridge stole focus");
                Disable(); PresenterSendMessage(handle, 0x0312, firstId, 0x7c << 16);
                Check(actions.Count == 12, "Disabled Stream Deck accepted queued keys");
            });
            Test("Stream Deck rolls back every registration if a key is already taken", () =>
            {
                Check(PresenterRegisterHotKey(handle, reserved, 0x4000, 0x7d), "Could not reserve F14");
                try
                {
                    Check(!Enable(out var error) && !string.IsNullOrWhiteSpace(error), "Conflicting key did not report failure");
                    Check(PresenterRegisterHotKey(handle, reserved + 1, 0x4000, 0x7c), "F13 remained captured after partial failure");
                    PresenterUnregisterHotKey(handle, reserved + 1);
                }
                finally { PresenterUnregisterHotKey(handle, reserved); }
            });
            Test("Stream Deck menu persists activation and controls actual projection and volume", () =>
            {
                bridge.Dispose();
                var menu = (MenuItem)window.FindName("StreamDeckEnabledMenu");
                if (menu is null) throw new InvalidOperationException("Stream Deck activation menu is missing");
                menu.IsChecked = true; Call(window, "ToggleStreamDeckClick", menu, new System.Windows.RoutedEventArgs());
                Check((bool)typeof(WorkspaceData).GetProperty("StreamDeckEnabled")!.GetValue(new WorkspaceStore().Load())!, "Stream Deck setting was not saved");
                var cue = new Cue { Kind = CueKind.Text, Title = "Stream Deck", Body = "Texto de prueba" };
                Call(window, "PrepareCue", cue, null);
                PresenterSendMessage(handle, 0x0312, firstId, 0x7c << 16);
                var session = Field<PresentationSession>(window, "_session");
                Check(session.Live?.Id == cue.Id, "Project key did not send the prepared cue to Programa");
                var volume = (Slider)window.FindName("VolumeSlider");
                volume.Value = 50;
                StreamDeckTestKey(0x83, 0, 0, 0);
                StreamDeckTestKey(0x83, 0, 2, 0);
                PumpFor(150);
                Check(volume.Value == 45, "Volume down did not adjust Culto Studio");
                PresenterSendMessage(handle, 0x0312, firstId + 8, 0x84 << 16);
                Check(volume.Value == 50, "Volume up did not adjust Culto Studio");
                menu.IsChecked = false; Call(window, "ToggleStreamDeckClick", menu, new System.Windows.RoutedEventArgs());
                Check(!(bool)typeof(WorkspaceData).GetProperty("StreamDeckEnabled")!.GetValue(new WorkspaceStore().Load())!, "Disabling Stream Deck was not saved");
                Check(PresenterRegisterHotKey(handle, reserved, 0x4000, 0x7c), "Disabling did not release F13");
                PresenterUnregisterHotKey(handle, reserved);
            });
            Test("Stream Deck image scene projects while another window keeps keyboard focus", () =>
            {
                var file = System.IO.Path.Combine(Field<WorkspaceStore>(window, "_store").DirectoryPath, "deck-image.png");
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96,
                    System.Windows.Media.PixelFormats.Bgr32, null, new byte[] { 0, 255, 0, 0 }, 4)));
                using (var output = System.IO.File.Create(file)) encoder.Save(output);
                var image = new Cue { Kind = CueKind.Image, Title = "Stream Deck image", FilePath = file };
                Field<WorkspaceData>(window, "_workspace").QuickSceneImage = image;
                var menu = (MenuItem)window.FindName("StreamDeckEnabledMenu");
                menu.IsChecked = true; Call(window, "ToggleStreamDeckClick", menu, new System.Windows.RoutedEventArgs());
                var other = new System.Windows.Window { Title = "Stream Deck focus test", Width = 320, Height = 180 };
                var topmost = window.Topmost;
                try
                {
                    window.Topmost = false; other.Show(); other.Activate(); PumpFor(100);
                    var foreground = new WindowInteropHelper(other).Handle;
                    Check(PresenterGetForegroundWindow() == foreground, "Could not focus the owned test window");
                    PresenterSendMessage(handle, 0x0312, firstId + 11, 0x87 << 16);
                    Check(Field<PresentationSession>(window, "_session").Live?.Id == image.Id, "Image scene did not project");
                    Check(PresenterGetForegroundWindow() == foreground, "Stream Deck image scene stole focus");
                }
                finally
                {
                    other.Close(); window.Topmost = topmost;
                    menu.IsChecked = false; Call(window, "ToggleStreamDeckClick", menu, new System.Windows.RoutedEventArgs());
                }
            });
        }
        finally
        {
            bridge.Dispose(); PresenterUnregisterHotKey(handle, reserved); PresenterUnregisterHotKey(handle, reserved + 1);
            if (typeof(MainWindow).GetMethod("DisposeStreamDeckHotkeys", Private) is { } dispose) dispose.Invoke(window, null);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void StreamDeckTestKey(byte key, byte scan, uint flags, nint extra);
}
