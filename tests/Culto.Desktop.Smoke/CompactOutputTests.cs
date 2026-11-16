using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunCompactOutputTests(MainWindow window)
    {
        Test("Combined monitor output keeps its video and output selector usable at compact sizes", () =>
        {
            var initial = (string)Call(window, "SerializeDockLayout")!;
            var width = window.Width; var height = window.Height;
            var session = Field<PresentationSession>(window, "_session");
            try
            {
                Call(window, "ApplyWorkspacePreset", "principal", false);
                Call(window, "PrepareCue", new Cue { Kind = CueKind.Video, Title = "Video compacto", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
                Call(window, "ProjectPrepared");
                var live = session.Live; var prepared = session.Prepared;
                foreach (var size in new[] { new Size(1280, 680), new Size(1480, 900), new Size(1600, 1000) })
                {
                    window.Width = size.Width; window.Height = size.Height; window.UpdateLayout(); PumpFor(80);
                    var panel = (FrameworkElement)window.FindName("LivePanel");
                    var viewport = (FrameworkElement)window.FindName("OutputViewport");
                    CheckWithin(viewport, panel, new Rect(panel.RenderSize), "Compact output picture");
                    Check(Math.Abs(viewport.ActualWidth / viewport.ActualHeight - 16d / 9) < .04, "Compact video lost 16:9 aspect ratio");
                    var scroll = (ScrollViewer)window.FindName("LiveScroll");
                    foreach (var name in new[] { "ChooseOutputButton", "ToggleOutputButton" })
                    {
                        var control = (FrameworkElement)window.FindName(name);
                        control.BringIntoView(); window.UpdateLayout(); PumpFor(20);
                        CheckWithin(control, scroll, new Rect(scroll.RenderSize), "Compact output control " + name);
                    }
                    Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Resizing replaced the presentation");
                }
            }
            finally { window.Width = width; window.Height = height; Call(window, "RestoreDockLayout", initial); }
        });
    }
}
