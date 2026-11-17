using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunStopBackgroundMediaTests(MainWindow window)
    {
        Test("Hidden video can be removed without changing the foreground and starts fresh when selected again", () => {
            var button = window.FindName("StopBackgroundMediaButton") as Button;
            Check(button is not null, "Missing stop-background-video action below Salidas");
            var row = (FrameworkElement)window.FindName("StopBackgroundMediaRow");
            Check(row.Visibility == Visibility.Collapsed, "Stop action visible without background video");
            ((Slider)window.FindName("VolumeSlider")).Value = 0;
            var loop = (CheckBox)window.FindName("LoopCheck");
            var previousLoop = loop.IsChecked;
            var clip = new Cue { Kind = CueKind.Video, Title = "Himno oculto", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
            var foreground = new Cue { Kind = CueKind.Text, Title = "Otra escena", Body = "La lectura continúa" };
            void Ready(Func<bool> ready, string error) {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (!ready() && DateTime.UtcNow < deadline) PumpFor(30);
                Check(ready(), error);
            }
            try {
                // Keep the short fixture alive while the foreground transitions.
                loop.IsChecked = true;
                Call(window, "PrepareCue", clip, null); Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                Ready(() => !output.IsTransitioning && output.IsPlaying, "Video did not start");
                var decoder = output.ActiveSurface;
                Call(window, "PrepareCue", foreground, null); Call(window, "ProjectPreparedPreservingOther");
                Ready(() => !output.IsTransitioning && output.HasBackgroundMedia && output.DisplayedCue?.Id == foreground.Id, "Scene did not retain hidden video");
                Call(window, "UpdateBackgroundAudioState");
                Check(row.Visibility == Visibility.Visible && button!.IsEnabled, "Hidden video has no stop action");
                window.UpdateLayout();
                var actions = (Grid)window.FindName("OutputActionsGrid");
                var selector = (FrameworkElement)window.FindName("OutputSelectorPill");
                Check(row.TranslatePoint(new Point(), actions).Y >= selector.TranslatePoint(new Point(), actions).Y + selector.ActualHeight,
                    "Stop action is not below Salidas");
                var previousSize = new Size(window.Width, window.Height);
                var productionPanel = (FrameworkElement)window.FindName("ProductionPanel");
                var previousHeight = productionPanel.Height;
                try
                {
                    productionPanel.Height = 340;
                    foreach (var size in new[] { new Size(1280, 680), new Size(1480, 900), new Size(1920, 1080) })
                    {
                        window.Width = size.Width; window.Height = size.Height; window.UpdateLayout(); PumpFor(100);
                        var production = (FrameworkElement)window.FindName("ProductionPanel");
                        var scroll = (ScrollViewer)window.FindName("LiveScroll");
                        var viewport = (FrameworkElement)window.FindName("OutputViewport");
                        var previewViewport = (FrameworkElement)window.FindName("PreviewViewport");
                        var pictureSize = viewport.RenderSize;
                        var previewSize = previewViewport.RenderSize;
                        row.Visibility = Visibility.Collapsed;
                        Call(window, "UpdateOutputPanelLayout"); window.UpdateLayout(); PumpFor(30);
                        Check(Math.Abs(viewport.ActualHeight - pictureSize.Height) < 1
                            && Math.Abs(previewViewport.ActualHeight - previewSize.Height) < 1,
                            "Showing background controls changes monitor size");
                        Call(window, "UpdateBackgroundAudioState"); window.UpdateLayout(); PumpFor(30);
                        CheckWithin(scroll, production, new Rect(production.RenderSize), "Output actions viewport");
                        scroll.ScrollToTop(); window.UpdateLayout(); PumpFor(30);
                        CheckWithin(button!, scroll, new Rect(scroll.RenderSize), "Stop video visible without scrolling");
                        button!.BringIntoView(); window.UpdateLayout(); PumpFor(30);
                        CheckWithin(button, scroll, new Rect(scroll.RenderSize), "Stop background video button");
                        CheckWithin(button, production, new Rect(production.RenderSize), "Stop video inside production card");
                        if (size.Width == 1480)
                            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "output-bottom-fixed.png"));
                    }
                }
                finally { productionPanel.Height = previousHeight; window.Width = previousSize.Width; window.Height = previousSize.Height; window.UpdateLayout(); }
                button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Ready(() => !output.IsPlaying && !output.HasBackgroundMedia, "Hidden playback did not stop");
                Check(output.RetainedMediaCue is null && output.AudioCue is null, "Stopped video is still retained");
                Check(output.DisplayedCue?.Id == foreground.Id, "Stopping hidden video replaced the foreground");
                Check(row.Visibility == Visibility.Collapsed, "Stop action stayed visible after removing the video");
                Call(window, "ReturnToMedia");
                Check(output.DisplayedCue?.Id == foreground.Id, "Return restored the removed video");
                Call(window, "PrepareCue", clip, null); Call(window, "ProjectPrepared");
                Ready(() => !output.IsTransitioning && output.IsPlaying, "Video did not start again");
                Check(!ReferenceEquals(decoder, output.ActiveSurface), "Video reused the removed decoder");
                Check(output.Position < 1, "Video resumed instead of starting at the beginning");
            }
            finally { loop.IsChecked = previousLoop; Call(window, "CloseOutput"); }
        });
        Test("Removing background video during a transition does not bring it back afterwards", () => {
            var workspace = Field<Culto.Desktop.Services.WorkspaceData>(window, "_workspace");
            var transitions = workspace.Transitions;
            var loop = (CheckBox)window.FindName("LoopCheck");
            var previousLoop = loop.IsChecked;
            void Ready(Func<bool> ready, string error) {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (!ready() && DateTime.UtcNow < deadline) PumpFor(30);
                Check(ready(), error);
            }
            try {
                loop.IsChecked = true;
                workspace.Transitions = transitions with { Kind = "Fade", DurationMilliseconds = 2000 };
                var clip = new Cue { Kind = CueKind.Video, Title = "Himno en transición", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
                var text = new Cue { Kind = CueKind.Text, Title = "Escena en transición", Body = "La escena permanece" };
                Call(window, "PrepareCue", clip, null); Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                Ready(() => !output.IsTransitioning && output.IsPlaying, "Video did not start");
                Call(window, "PrepareCue", text, null); Call(window, "ProjectPreparedPreservingOther");
                Ready(() => output.IsTransitioning && output.HasBackgroundMedia && output.DisplayedCue?.Id == text.Id, "No retained video during transition");
                Call(window, "UpdateBackgroundAudioState");
                ((Button)window.FindName("StopBackgroundMediaButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpFor(2300);
                Check(!output.HasBackgroundMedia && output.RetainedMediaCue is null && !output.IsPlaying, "Transition restored removed video");
                Check(output.DisplayedCue?.Id == text.Id, "Stop changed the foreground during its transition");
            }
            finally { workspace.Transitions = transitions; loop.IsChecked = previousLoop; Call(window, "CloseOutput"); }
        });
    }
}
