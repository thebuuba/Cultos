using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;
using System.Windows.Media;

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
                    Check(Math.Abs(viewport.ActualWidth / viewport.ActualHeight - ((AspectRatioBox)window.FindName("LivePicture")).AspectRatio) < .04, "Compact video lost output aspect ratio");
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
        Test("Monitor shape and media framing follow the output window without replacing playback", () =>
        {
            Call(window, "CloseOutput");
            var cue = new Cue { Kind = CueKind.Video, Title = "Video HDMI", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
            try
            {
                Call(window, "PrepareCue", cue, null); Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                Until(() => output.IsPlaying && !output.IsTransitioning, "Local video did not start");
                var decoder = Field<LibVLCSharp.Shared.MediaPlayer>(output, "_player");
                var device = DisplayService.GetDisplays().First();
                Call(window, "OpenOutput", device, true); PumpFor(120);
                var external = Field<Window>(window, "_outputWindow");
                foreach (var shape in new[] { new Size(850, 700), new Size(1050, 450) })
                {
                    external.Width = shape.Width; external.Height = shape.Height;
                    external.UpdateLayout(); window.UpdateLayout(); PumpFor(70);
                    Call(window, "UpdateMonitorGeometry"); window.UpdateLayout();
                    var ratio = output.ActualWidth / output.ActualHeight;
                    Check(Math.Abs(((AspectRatioBox)window.FindName("LivePicture")).AspectRatio - ratio) < .01
                        && Math.Abs(((AspectRatioBox)window.FindName("PreviewPicture")).AspectRatio - ratio) < .01,
                        "Monitors retain a fixed shape after output resolution changes");
                }
                output.MediaFill = true; output.MediaZoom = 1.5;
                var pixels = Field<Image>(output, "_compositedVideo");
                Check(pixels.Stretch == Stretch.UniformToFill && pixels.RenderTransform is ScaleTransform { ScaleX: 1.5 }, "Video framing was not applied to the actual output");
                Check(ReferenceEquals(decoder, Field<LibVLCSharp.Shared.MediaPlayer>(output, "_player")) && output.IsPlaying, "HDMI or framing replaced or paused the decoder");
                output.MediaFill = false; output.MediaZoom = 1;
                Call(window, "CloseExternalOutput"); PumpFor(100);
                Check(ReferenceEquals(decoder, Field<LibVLCSharp.Shared.MediaPlayer>(output, "_player")) && output.IsPlaying, "Closing HDMI interrupted the local video");
                var second = cue.Copy(); second.Id = Guid.NewGuid(); second.Title = "Himno HDMI";
                Call(window, "OpenOutput", device, true);
                Call(window, "PrepareCue", second, null); Call(window, "ProjectPrepared");
                Until(() => output.DisplayedCue?.Id == second.Id && output.IsPlaying && !output.IsTransitioning, "A second hymn/video failed while HDMI was active");
            }
            finally { Call(window, "CloseOutput"); }
        });
    }
}
