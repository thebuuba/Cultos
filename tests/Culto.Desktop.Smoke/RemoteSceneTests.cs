using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;
using Culto.Remote;

internal static partial class Program
{
    private static void RunRemoteSceneTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var session = Field<PresentationSession>(window, "_session");
        RemoteSnapshot Snapshot() => (RemoteSnapshot)Call(window, "BuildRemoteState")!;
        CommandResult Scene(string id) => (CommandResult)Call(window, "ExecuteRemote",
            new RemoteCommand("scene", Guid.NewGuid().ToString("N"), ButtonId: id))!;
        Test("Phone publishes YouTube and desktop scenes even with closed output", () =>
        {
            Call(window, "CloseOutput");
            var scenes = Snapshot().Scenes!;
            foreach (var id in new[] { "logo", "blackout", "image", "video", "youtube" })
                Check(scenes.Any(s => s.Id == id), "Phone has no scene " + id);
            Check(scenes.Single(s => s.Id == "blackout").Available, "Blackout cannot open closed output");
            var before = session.Live;
            Check(!Scene("not-a-scene").Success && ReferenceEquals(before, session.Live), "Unknown scene changed output");
        });
        Test("Phone scene projects remembered image without an order entry or file picker", () =>
        {
            var path = Path.Combine(Field<WorkspaceStore>(window, "_store").DirectoryPath, "remote-scene.png");
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgr32, null,
                new byte[] { 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0 }, 8)));
            using (var file = File.Create(path)) png.Save(file);
            var image = new Cue { Kind = CueKind.Image, Title = "Escena imagen del teléfono", FilePath = path };
            workspace.QuickSceneImage = image;
            Call(window, "PrepareCue", new Cue { Body = "Otra vista previa" }, null);
            Check(Scene("image").Success, "Phone image scene was rejected");
            Check(session.Live?.Id == image.Id, "Phone selected another image");
            Check(Snapshot().Scenes!.Single(s => s.Id == "image").Active, "Phone image scene lacks live marker");
            Check(System.Windows.Automation.AutomationProperties.GetItemStatus((Button)window.FindName("SceneImageButton")) == "En pantalla",
                "Desktop image scene lacks live marker");
            Check(Scene("blackout").Success && Snapshot().Scenes!.Single(s => s.Id == "blackout").Active,
                "Phone blackout scene failed");
            Check(Scene("blackout").Success && !Snapshot().IsBlackout, "Phone cannot restore from black");
            File.Delete(path);
            var live = session.Live;
            Check(!Scene("image").Success && ReferenceEquals(live, session.Live), "Missing scene file replaced live content");
        });
        Test("Scene deck keeps large colored keys and visible focus", () =>
        {
            foreach (var name in new[] { "ChurchBackgroundButton", "BlackoutButton", "SceneImageButton", "SceneVideoButton", "SceneYouTubeButton" })
            {
                var button = (Button)window.FindName(name);
                Check(button.Height >= 64 && button.MinWidth >= 44, "Deck key too small: " + name);
            }
            Check(((SolidColorBrush)((Button)window.FindName("SceneYouTubeButton")).Background).Color == Colors.White,
                "YouTube key lost its white surface");
            var folder = Path.Combine(Environment.CurrentDirectory, ".tools", "deckboard"); Directory.CreateDirectory(folder);
            CaptureDesignWindow(window, Path.Combine(folder, "desktop-scenes.png"));
        });
        Test("Phone YouTube scene captures its selected Chrome window without an order entry", () =>
        {
            Call(window, "CloseOutput");
            ((Slider)window.FindName("VolumeSlider")).Value = 0;
            var folder = Path.Combine(Environment.CurrentDirectory, ".tools", "deckboard-chrome-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var title = "Culto Deckboard owned YouTube " + Guid.NewGuid().ToString("N");
            var page = Path.Combine(folder, "index.html");
            File.WriteAllText(page, "<!doctype html><title>" + title + "</title><body style='margin:0;background:lime'><h1>Owned scene test</h1>");
            var start = new ProcessStartInfo(@"C:\Program Files\Google\Chrome\Application\chrome.exe")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Normal };
            foreach (var arg in new[] { "--user-data-dir=" + Path.Combine(folder, "profile"), "--no-first-run", "--no-default-browser-check", "--app=" + new Uri(page).AbsoluteUri })
                start.ArgumentList.Add(arg);
            using var chrome = Process.Start(start)!;
            try
            {
                Until(() => { Call(window, "RefreshYouTube", (object?)null); return ((ListBox)window.FindName("YouTubeList")).Items.OfType<YouTubeSource>().Any(s => s.Title.Contains(title)); },
                    "Owned Chrome source was not discovered");
                var selected = ((ListBox)window.FindName("YouTubeList")).Items.OfType<YouTubeSource>().Single(s => s.Title.Contains(title));
                ((ListBox)window.FindName("YouTubeList")).SelectedItem = selected;
                Check(Field<object?>(Field<Culto.Desktop.Projection.ProjectionSurface>(window, "_preview"), "_captureAudio") is null,
                    "Preparing YouTube still acquires a muting Chrome audio lease");
                Check(Snapshot().Scenes!.Single(s => s.Id == "youtube").Available, "Phone YouTube scene is unavailable");
                Check(Scene("youtube").Success, "Phone YouTube command was rejected");
                Check(session.Live?.Capture?.WindowHandle == selected.Cue.Capture?.WindowHandle, "Phone captured a different browser");
                var output = Field<Culto.Desktop.Projection.ProjectionSurface>(window, "_output");
                Until(() => output.IsPlaying && !output.IsTransitioning, "YouTube scene never received source pixels");
                Check(Field<object?>(output, "_captureAudio") is null, "Projecting YouTube still takes control of Chrome audio");
                var state = Snapshot();
                Check(state.Scenes!.Single(s => s.Id == "youtube").Active && !state.CanPause,
                    "Phone live YouTube state or transport is incorrect");
                SetOwnedChromeTitle((nint)selected.Cue.Capture!.WindowHandle, "Other owned Chrome tab");
                Until(() =>
                {
                    var bitmap = Field<BitmapSource?>(output, "_captureBitmap"); if (bitmap is null) return false;
                    var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(640, 360, 1, 1), pixel, 4, 0);
                    return pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 0;
                }, "Leaving the YouTube tab did not protect the projected picture");
                Check(Field<object?>(output, "_captureAudio") is null, "Tab gating still mutes browser audio");
                SetOwnedChromeTitle((nint)selected.Cue.Capture.WindowHandle, title);
                Call(window, "PrepareCue", new Cue { Kind = CueKind.Bible, Body = "Reading while Chrome audio stays independent" }, null);
                Call(window, "ProjectPrepared");
                Until(() => !output.IsTransitioning && output.DisplayedCue?.Kind == CueKind.Bible, "Reading did not replace YouTube");
            }
            finally
            {
                Call(window, "CloseOutput");
                if (!chrome.HasExited) chrome.Kill(entireProcessTree: true);
            }
        });
        Call(window, "CloseOutput");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetOwnedChromeTitle(nint window, string title);
}
