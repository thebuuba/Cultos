using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunOutputControlTests(MainWindow window)
    {
        Test("External attach and close keep the same playing local video", () =>
        {
            Call(window, "CloseOutput");
            var video = new Cue { Title = "Traspaso local", Kind = CueKind.Video, FilePath = System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
            Call(window, "PrepareCue", video, null); Call(window, "ProjectPrepared");
            var renderer = Field<ProjectionSurface>(window, "_output"); renderer.Loop = true;
            Until(() => renderer.IsPlaying && !renderer.IsTransitioning, "Local video did not begin");
            var prepared = Field<PresentationSession>(window, "_session").Prepared;
            var before = renderer.Position;
            Call(window, "OpenOutput", DisplayService.GetDisplays().First(), true);
            var external = Field<ProjectionWindow>(window, "_outputWindow");
            Check(ReferenceEquals(renderer, external.Content) && renderer.IsPlaying && renderer.Position >= before - .1, "Opening HDMI restarted/replaced local playback");
            Until(() => ((OutputMonitor)window.FindName("LiveMonitor")).IsConnected, "HDMI monitor did not connect");
            external.Close(); window.UpdateLayout();
            Check(Field<ProjectionWindow?>(window, "_outputWindow") is null && ReferenceEquals(renderer, Field<ProjectionSurface>(window, "_output")) && renderer.IsPlaying,
                "Closing external output stopped the local video");
            Check(ReferenceEquals(prepared, Field<PresentationSession>(window, "_session").Prepared), "External handoff changed the preview");
            Call(window, "ToggleBlackout");
            Call(window, "OpenOutput", DisplayService.GetDisplays().First(), true);
        });
        Test("Output selection excludes the console even with a stale saved device or a secondary console", () =>
        {
            var main = new DisplayInfo("main", "Principal", 0, 0, 1920, 1080, true);
            var hdmi = new DisplayInfo("hdmi", "HDMI", 1920, 0, 1920, 1080, false);
            var third = hdmi with { DeviceName = "third" };
            Check(DisplayService.SelectOutput([main], "main", "main") is null, "Single monitor is eligible for projection");
            Check(DisplayService.SelectOutput([main, hdmi], "main", "main") == hdmi, "Saved console device overrides external output");
            Check(DisplayService.SelectOutput([main, hdmi], "hdmi", "hdmi") == main, "Primary church display cannot be used with a secondary console");
            Check(DisplayService.SelectOutput([main, hdmi, third], "third", "main") == third, "Saved external selection was ignored");
            Check(DisplayService.SelectOutput([main], "hdmi", "main") is null, "Disconnected HDMI falls back to console");
        });
        Test("Inline output switch disconnects the real monitor and preserves the prepared cue", () =>
        {
            var session = Field<PresentationSession>(window, "_session");
            var live = session.Live;
            var prepared = session.Prepared;
            var button = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("ToggleOutputButton");
            var renderer = Field<ProjectionSurface>(window, "_output");
            Check(button.IsChecked == true && button.ToolTip.ToString() == "Desactivar salida", "Active output switch is not synchronized");
            Check(((TextBlock)window.FindName("OutputDisplayName")).Text.StartsWith("Pantalla "), "Selected display is absent from the output pill");
            window.UpdateLayout();
            var footer = (ScrollViewer)window.FindName("LiveScroll");
            CheckWithin((FrameworkElement)window.FindName("OutputSelectorPill"), footer, new Rect(footer.RenderSize), "Output display and switch");
            CaptureDesignWindow(window, System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "output-pill-active.png"));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Field<ProjectionWindow?>(window, "_outputWindow") is null && !((OutputMonitor)window.FindName("LiveMonitor")).IsConnected, "Stop left output active");
            Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Stop changed the session");
            Check(button.IsChecked == false && ((TextBlock)window.FindName("LiveTitleText")).Text == (live?.Title ?? "Sin contenido"), "Disabling HDMI cleared local output or left the switch on");
            Check(ReferenceEquals(renderer, Field<ProjectionSurface>(window, "_output")), "Disabling HDMI replaced the player");
            if (DisplayService.GetDisplays().Count == 1)
            {
                button.IsChecked = true;
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Field<ProjectionWindow?>(window, "_outputWindow") is null, "Activate opened a rehearsal window");
                Check(button.IsChecked == false, "Unavailable output left the switch on");
            }
            Call(window, "OpenOutput", DisplayService.GetDisplays().First(), true);
            var output = Field<ProjectionWindow>(window, "_outputWindow");
            Check(!output.ShowInTaskbar && !output.ShowActivated, "Projection competes with the operator window");
            Check(ReferenceEquals(renderer, output.Content), "HDMI did not use the local renderer");
            Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Reopening projected the preview");
            Check(((TextBlock)window.FindName("LiveTitleText")).Text == (live?.Title ?? "Sin contenido"), "Reopened output title disagrees with restored content");
        });
    }
}
