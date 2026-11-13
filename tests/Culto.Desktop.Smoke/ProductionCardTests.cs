using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunProductionDemo(MainWindow window)
    {
        window.Title = "Culto Studio — revisión de monitores";
        window.Width = 1480; window.Height = 900;
        Call(window, "ApplyWorkspacePreset", "principal", false);
        ((Slider)window.FindName("VolumeSlider")).Value = 0;
        ((CheckBox)window.FindName("LoopCheck")).IsChecked = true;
        var cue = new Cue { Kind = CueKind.Video, Title = "Himno de prueba", FilePath = Environment.GetEnvironmentVariable("CULTO_DEMO_VIDEO") ?? Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
        Call(window, "PrepareCue", cue, null);
        ((Button)window.FindName("SceneVideoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(() => Field<ProjectionSurface>(window, "_output").IsPlaying, "Demo video did not play");
        Console.WriteLine("PRODUCTION DEMO READY");
        PumpFor(55000);
    }

    private static void RunProductionCardTests(MainWindow window)
    {
        var dock = (DockingManager)window.FindName("WorkspaceDock");
        var session = Field<PresentationSession>(window, "_session");
        ((Slider)window.FindName("VolumeSlider")).Value = 0;
        Call(window, "ApplyWorkspacePreset", "principal", false);
        window.Width = 1800; window.Height = 950; window.UpdateLayout(); PumpFor(100);
        Test("Scenes and the two monitors share one card in left-to-right order", () =>
        {
            var scene = (FrameworkElement?)window.FindName("ScenesPanel");
            Check(scene is not null, "Scenes panel is missing");
            var preview = (FrameworkElement)window.FindName("PreviewPanel");
            var live = (FrameworkElement)window.FindName("LivePanel");
            var group = Descendants(dock).OfType<LayoutPanelControl>().Last(p => p.IsAncestorOf(scene!) && p.IsAncestorOf(preview) && p.IsAncestorOf(live));
            Check(group.Effect is not null && group.Clip is not null, "Production group has no shared card");
            Check(BoundsWithin(scene!, dock).Right <= BoundsWithin(preview, dock).Left + 1
                && BoundsWithin(preview, dock).Right <= BoundsWithin(live, dock).Left + 1, "Scenes and monitors are not side by side");
        });
        Test("Older eight-panel layouts acquire Scenes without replacing their content", () =>
        {
            var original = dock.Layout.Descendents().OfType<LayoutAnchorable>().ToDictionary(p => p.ContentId, p => p.Content);
            var xml = XDocument.Parse((string)Call(window, "SerializeDockLayout")!);
            xml.Descendants("LayoutAnchorable").Single(e => (string?)e.Attribute("ContentId") == "scenes").Parent!.Remove();
            xml.Root!.SetAttributeValue("WorkspacePreset", "principal");
            Call(window, "RestoreDockLayout", xml.ToString()); window.UpdateLayout();
            var panels = dock.Layout.Descendents().OfType<LayoutAnchorable>().ToArray();
            Check(Field<bool>(window, "_adaptPrincipalWorkspace"), "Migration disabled adaptive Principal layout");
            Check(panels.Length == 9 && panels.Count(p => p.ContentId == "scenes") == 1, "Scenes migration did not complete");
            foreach (var entry in original) Check(ReferenceEquals(panels.Single(p => p.ContentId == entry.Key).Content, entry.Value), "Migration recreated " + entry.Key);
        });
        Test("Image scene remembers its choice and recalls it after preparing other content", () =>
        {
            var button = window.FindName("SceneImageButton") as Button;
            Check(button is not null, "Image scene is missing");
            var path = Path.Combine(Field<WorkspaceStore>(window, "_store").DirectoryPath, "scene-test.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgr32, null, new byte[16], 8)));
            using (var file = File.Create(path)) encoder.Save(file);
            var image = MediaLibrary.FromFile(path);
            var previous = session.Live;
            Call(window, "PrepareCue", image, null);
            Check(ReferenceEquals(previous, session.Live), "Preparing the image projected it");
            button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(session.Live?.FilePath == path, "Image scene did not project the selected image");
            Call(window, "PrepareCue", new Cue { Title = "Another prepared item", Body = "Separate preview" }, null);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(session.Live?.FilePath == path, "Image scene lost its remembered choice");
            using var saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "workspace.json")));
            Check(saved.RootElement.TryGetProperty("QuickSceneImage", out var stored) && stored.GetProperty("FilePath").GetString() == path, "Image scene is not persistent");
        });
        Test("One local transport moves between monitors and never becomes projected content", () =>
        {
            var viewport = (FrameworkElement)window.FindName("OutputViewport");
            var transport = (FrameworkElement)window.FindName("MediaPlaybackPanel");
            Call(window, "PlacePlaybackOverlay", window.FindName("LiveOverlayHost")); window.UpdateLayout();
            Check(viewport.IsAncestorOf(transport), "Transport remains outside the monitor picture");
            var video = new Cue { Kind = CueKind.Video, Title = "Production transport", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
            Call(window, "PrepareCue", video, null); Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            Until(() => output.IsPlaying && !output.IsTransitioning, "Production video did not start");
            Call(window, "ShowPlaybackOverlay", true); PumpFor(180);
            Check(transport.Opacity > .9 && transport.IsHitTestVisible, "Hovered transport is unavailable");
            var folder = Path.Combine(Environment.CurrentDirectory, ".tools", "visual-theme"); Directory.CreateDirectory(folder);
            CaptureDesignWindow(window, Path.Combine(folder, "production-controls.png"));
            var previewHost = (ContentControl)window.FindName("PreviewOverlayHost");
            Call(window, "PlacePlaybackOverlay", previewHost); window.UpdateLayout();
            Check(((FrameworkElement)window.FindName("PreviewViewport")).IsAncestorOf(transport), "Preview did not receive the same transport");
            CaptureDesignWindow(window, Path.Combine(folder, "production-preview-controls.png"));
            ((Button)window.FindName("PauseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(() => output.IsPaused, "Preview transport did not pause the live player");
            Check(!Descendants(output).OfType<Button>().Any() && !output.IsAncestorOf(transport), "Operator controls entered the projection");
            Call(window, "PrepareCue", new Cue { Title = "Browse", Body = "Separate preview" }, null);
            window.UpdateLayout();
            Check(viewport.IsAncestorOf(transport), "Browsing left the output transport on an unrelated preview");
            Call(window, "ShowPlaybackOverlay", false); PumpFor(180);
            Check(transport.Opacity < .01 && !transport.IsHitTestVisible, "Unhovered transport stays visible or blocks clicks");
        });
        Test("Scene transition selector preserves timing and matches settings refresh", () =>
        {
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var original = workspace.Transitions;
            var picker = (ComboBox)window.FindName("SceneTransitionPicker");
            picker.SelectedItem = picker.Items.OfType<ComboBoxItem>().Single(item => (string)item.Tag == "Slide");
            Check(workspace.Transitions.Kind == "Slide" && workspace.Transitions.DurationMilliseconds == original.DurationMilliseconds
                && workspace.Transitions.AudioFadeInMilliseconds == original.AudioFadeInMilliseconds
                && workspace.Transitions.AudioFadeOutMilliseconds == original.AudioFadeOutMilliseconds, "Scene selector replaced transition timing");
            workspace.Transitions = original with { Kind = "Zoom" };
            Call(window, "RefreshSceneChoices");
            Check(((ComboBoxItem)picker.SelectedItem).Tag as string == "Zoom", "Settings changes did not update the scene selector");
            workspace.Transitions = original; Call(window, "RefreshSceneChoices");
        });
        Test("Scene discovery remains active without preparing or projecting browser windows", () =>
        {
            Call(window, "ActivateWorkspacePanel", "sources"); window.UpdateLayout();
            var live = session.Live; var prepared = session.Prepared;
            var timer = Field<System.Windows.Threading.DispatcherTimer>(window, "_youTubeTimer");
            Check(((FrameworkElement)window.FindName("ScenesPanel")).IsVisible && timer.IsEnabled, "Scenes stopped browser discovery");
            var menu = ((Button)window.FindName("SceneYouTubeButton")).ContextMenu;
            Call(window, "SceneYouTubeMenuOpened", menu, new RoutedEventArgs());
            Check(menu.Items.OfType<MenuItem>().Any(item => item.Header?.ToString() == "Elegir otra ventana…"), "YouTube scene cannot choose another window");
            Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Discovery or opening the scene menu changed output");
        });
        Test("Keyboard use reveals a transport hidden after mouse interaction", () =>
        {
            Call(window, "FollowLivePreview");
            Call(window, "PlacePlaybackOverlay", window.FindName("LiveOverlayHost")); window.UpdateLayout(); window.Activate();
            var transport = (FrameworkElement)window.FindName("MediaPlaybackPanel");
            Check(((Button)window.FindName("PauseButton")).Focus(), "Transport could not receive keyboard focus");
            Call(window, "ShowPlaybackOverlay", false); PumpFor(180);
            transport.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, Environment.TickCount, Key.Space)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            PumpFor(180);
            Check(transport.Opacity > .9 && transport.IsHitTestVisible, "Keyboard interaction kept the focused transport hidden");
        });
        Call(window, "CloseOutput");
    }
}
