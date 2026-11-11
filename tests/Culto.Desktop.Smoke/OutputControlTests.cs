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
            var button = (Button)window.FindName("ToggleOutputButton");
            Check(button.Content.ToString() == "Desactivar salida", "Active output has no stop control");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Field<ProjectionWindow?>(window, "_outputWindow") is null && !((OutputMonitor)window.FindName("LiveMonitor")).IsConnected, "Stop left output active");
            Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Stop changed the session");
            Check(button.Content.ToString() == "Activar salida" && ((TextBlock)window.FindName("LiveTitleText")).Text == "Sin contenido", "Closed output displays a stale live title");
            if (DisplayService.GetDisplays().Count == 1)
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Field<ProjectionWindow?>(window, "_outputWindow") is null, "Activate opened a rehearsal window");
            }
            Call(window, "OpenOutput", DisplayService.GetDisplays().First(), true);
            var output = Field<ProjectionWindow>(window, "_outputWindow");
            Check(!output.ShowInTaskbar && !output.ShowActivated, "Projection competes with the operator window");
            Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Reopening projected the preview");
            Check(((TextBlock)window.FindName("LiveTitleText")).Text == (live?.Title ?? "Sin contenido"), "Reopened output title disagrees with restored content");
        });
    }
}
