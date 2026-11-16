using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Remote;

internal static partial class Program
{
    private static void RunYouTubeControlsTests(MainWindow window)
    {
        var session = Field<PresentationSession>(window, "_session");
        var volume = (Slider)window.FindName("VolumeSlider");
        var oldVolume = volume.Value;
        Window? floating = null;
        try
        {
            volume.Value = 0;
            Test("YouTube cards show live capture without a green selection border and release hidden previews", () =>
            {
                var folder = Path.Combine(Path.GetTempPath(), "CultoThumbnail-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                var title = "YouTube thumbnail " + Guid.NewGuid().ToString("N");
                var page = Path.Combine(folder, "index.html");
                File.WriteAllText(page, $"<title>{title}</title><body style='background:red'><script>setInterval(()=>document.body.style.background=document.body.style.background==='red'?'blue':'red',2000)</script>");
                using var chrome = System.Diagnostics.Process.Start(YouTubeProjectionBrowser.CreateStartInfo(
                    @"C:\Program Files\Google\Chrome\Application\chrome.exe", Path.Combine(folder, "profile"), new Uri(page)))!;
                var list = (ListBox)window.FindName("YouTubeList");
                var timer = Field<DispatcherTimer>(window, "_youTubeTimer");
                try
                {
                    Until(() => WindowCaptureCatalog.List().Any(t => t.Options.WindowTitle.StartsWith(title)), "Owned Chrome did not open");
                    Call(window, "SelectContentSource", 3); timer.Stop();
                    var target = WindowCaptureCatalog.List().Single(t => t.Options.WindowTitle.StartsWith(title));
                    var source = list.Items.OfType<YouTubeSource>().Single(s => s.Cue.Capture!.WindowHandle == target.Options.WindowHandle);
                    list.SelectedItem = source; list.ScrollIntoView(source); list.UpdateLayout();
                    var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(source);
                    var thumbnail = Descendants(row).OfType<FrameworkElement>()
                        .SingleOrDefault(e => e.GetType().Name == "WindowCaptureThumbnail");
                    Check(thumbnail is not null, "YouTube card still renders a title cover instead of live capture");
                    var border = Descendants(row).OfType<Border>().First();
                    Check(border.BorderThickness == new Thickness(0), "YouTube selection still has an outline");
                    Until(() => Descendants(thumbnail!).OfType<ProjectionSurface>().Any(s => Field<BitmapSource?>(s, "_captureBitmap") is not null),
                        "YouTube thumbnail received no captured frame");
                    var surface = Descendants(thumbnail!).OfType<ProjectionSurface>().Single();
                    int Pixel()
                    {
                        var pixel = new byte[4];
                        Field<BitmapSource>(surface, "_captureBitmap").CopyPixels(new Int32Rect(640, 360, 1, 1), pixel, 4, 0);
                        return pixel[0] | pixel[1] << 8 | pixel[2] << 16;
                    }
                    Until(() => Pixel() == 0xFF0000, "Thumbnail did not show the source window");
                    Until(() => Pixel() == 0x0000FF, "Thumbnail froze instead of updating live");
                    Check(Field<object?>(surface, "_captureAudio") is null, "Thumbnail took control of application audio");
                    Call(window, "RefreshYouTube", (object?)null); list.UpdateLayout();
                    Check(ReferenceEquals(list.ItemContainerGenerator.ContainerFromItem(source), row)
                        && ReferenceEquals(Descendants(thumbnail!).OfType<ProjectionSurface>().Single(), surface),
                        "Discovery restarted an unchanged thumbnail");
                    var thumbnailClient = Field<object>(surface, "_captureClient");
                    var refreshCount = Field<int>(thumbnailClient, "_refreshCount");
                    Call(window, "RefreshBrowserCaptureAfterResize", null, EventArgs.Empty);
                    Until(() => Field<int>(thumbnailClient, "_refreshCount") > refreshCount,
                        "Capture recovery skipped the YouTube thumbnail");
                    thumbnail!.Visibility = Visibility.Collapsed;
                    Until(() => !Descendants(thumbnail).OfType<ProjectionSurface>().Any(), "Hidden thumbnail kept its capture alive");
                    Check(Field<object?>(surface, "_captureClient") is null, "Hidden thumbnail did not dispose its worker");
                    thumbnail.Visibility = Visibility.Visible;
                    Until(() => Descendants(thumbnail).OfType<ProjectionSurface>().Any(s => Field<BitmapSource?>(s, "_captureBitmap") is not null),
                        "Showing thumbnail did not resume capture");
                }
                finally
                {
                    list.ItemsSource = null;
                    if (!chrome.HasExited) chrome.Kill(entireProcessTree: true);
                    Call(window, "PrepareCue", new Cue { Kind = CueKind.Text, Title = "Fin de prueba" }, null);
                    Call(window, "RefreshYouTube", (object?)null); timer.Start();
                }
            });
            Test("Capture playback has no fake pause in console, floating player or remote", () =>
            {
                Call(window, "EnsureOutput");
                var output = Field<ProjectionSurface>(window, "_output");
                output.SetPaused(false);
                session.RestoreOutput(new Cue { Kind = CueKind.WindowCapture, Title = "YouTube de prueba" }, false, false);
                Call(window, "UpdateUsabilityState");
                var before = output.IsPaused;
                Call(window, "TogglePause");
                Check(output.IsPaused == before, "Capture pause changed output playback");
                var pause = (Button)window.FindName("PauseButton");
                Check(!pause.IsEnabled && pause.Visibility == Visibility.Collapsed, "Console exposes a capture pause button");
                Check(!((RemoteSnapshot)Call(window, "BuildRemoteState")!).CanPause, "Phone advertises capture pause support");
                var result = (CommandResult)Call(window, "ExecuteRemote", new RemoteCommand("pause", Guid.NewGuid().ToString()))!;
                Check(!result.Success, "Phone accepted a fake capture pause");
                Call(window, "OpenOutputPlayer"); floating = Field<Window>(window, "_outputPlayerWindow");
                var floatingPause = (Button)floating.FindName("PlayerPause");
                Check(!floatingPause.IsEnabled && floatingPause.Visibility == Visibility.Collapsed, "Floating player exposes capture pause");
                floating.Close(); floating = null;
                Call(window, "CloseOutput");
            });
            Test("YouTube opens its independent native panel without starting capture", () =>
            {
                var manager = (DockingManager)window.FindName("WorkspaceDock");
                var pane = manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(item => item.ContentId == "youtube");
                var panel = (FrameworkElement)window.FindName("YouTubePanel");
                var library = (Button)window.FindName("LibraryMenuButton");
                Check(!library.ContextMenu!.Items.OfType<MenuItem>().Any(item => item.Header?.ToString()?.Contains("Capturar ventana") == true),
                    "Capture still lives in Library menu");
                Call(window, "SelectContentSource", 3);
                Until(() => Field<int>(window, "_activeContentSource") == 3 && pane.IsActive && panel.IsVisible
                    && ((ListBox)window.FindName("YouTubeList")).IsKeyboardFocusWithin, "YouTube shortcut did not activate and focus its native panel");
                Check(ReferenceEquals(manager.ActiveContent, panel), "YouTube activation opened a different panel");
                Check(Field<object?>(Field<ProjectionSurface>(window, "_preview"), "_captureClient") is null,
                    "Opening YouTube started an unrequested capture");
            });
            Test("YouTube discovery refreshes on native activation and runs while YouTube or Scenes is visible", () =>
            {
                var panel = (FrameworkElement)window.FindName("YouTubePanel");
                var timer = Field<DispatcherTimer>(window, "_youTubeTimer");
                var count = (TextBlock)window.FindName("YouTubeCountText");
                Call(window, "ActivateWorkspacePanel", "order");
                count.Text = "Pending native activation refresh";
                Call(window, "ActivateWorkspacePanel", "youtube");
                Check(count.Text != "Pending native activation refresh", "Activating an already visible YouTube panel did not rediscover windows");
                var visibility = panel.Visibility;
                var scenes = (FrameworkElement)window.FindName("ScenesPanel");
                var scenesVisibility = scenes.Visibility;
                try
                {
                    panel.Visibility = Visibility.Collapsed;
                    Check(timer.IsEnabled, "Scenes stopped discovering available YouTube windows");
                    scenes.Visibility = Visibility.Collapsed;
                    Until(() => !timer.IsEnabled, "Hidden YouTube panel kept polling windows");
                }
                finally { panel.Visibility = visibility; scenes.Visibility = scenesVisibility; }
                Until(() => panel.IsVisible && timer.IsEnabled, "Showing YouTube did not resume discovery");
            });
            Test("Keep audio and one-way return work through the actual console actions", () =>
            {
                var video = new Cue { Kind = CueKind.Video, Title = "Conservar este video", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
                Call(window, "PrepareCue", video, null); Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                Until(() => output.IsPlaying && output.Position > .2 && !output.IsTransitioning, "Video did not start");
                var retained = output.ActiveSurface;
                var position = output.Position;
                var text = new Cue { Kind = CueKind.Text, Title = "Texto encima", Body = "La mÃºsica continÃºa" };
                Call(window, "PrepareCue", text, null); Call(window, "ProjectPreparedPreservingOther");
                Until(() => output.HasBackgroundMedia && !output.IsTransitioning, "Console did not retain background media");
                PumpFor(450); Call(window, "TimerTick", null, EventArgs.Empty);
                Check(output.IsPlaying && output.Position > position, "Video stopped while text was shown");
                Check(((Button)window.FindName("ReturnToMediaButton")).Visibility == Visibility.Visible
                    && ((FrameworkElement)window.FindName("MediaPlaybackPanel")).Visibility == Visibility.Visible,
                    "Return action or background volume is missing");
                Call(window, "ReturnToMedia");
                Until(() => session.Live?.Id == video.Id && !output.IsTransitioning, "Return did not show the retained video");
                Check(ReferenceEquals(retained, output.ActiveSurface) && output.Position > position, "Return replaced or restarted the decoder");
                Call(window, "ReturnToMedia");
                Check(session.Live?.Id == video.Id, "Repeated return toggled away from video");
            });
        }
        finally
        {
            floating?.Close(); Call(window, "DisposeYouTube"); Call(window, "CloseOutput");
            volume.Value = oldVolume;
        }
    }
}
