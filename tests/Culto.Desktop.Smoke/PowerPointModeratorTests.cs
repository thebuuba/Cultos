using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunPowerPointModeratorTests(MainWindow window, string directory)
    {
        Test("Moderator view has an entry point that requires a live PowerPoint", () =>
        {
            Check(window.FindName("PowerPointModeratorButton") is Button { IsEnabled: false },
                "The PowerPoint moderator button is missing or enabled without a presentation");
        });
        if (window.FindName("PowerPointModeratorButton") is not Button open) return;
        if (!PowerPointPresentation.IsAvailable) { Console.WriteLine("SKIP moderator native tests: PowerPoint is not installed"); return; }
        dynamic office = Activator.CreateInstance(Type.GetTypeFromProgID("PowerPoint.Application")!)!;
        dynamic decks = office.Presentations;
        var count = (int)decks.Count;
        var fixture = Path.Combine(directory, "moderator.pptx");
        Window? moderator = null;
        try
        {
            CreatePowerPointFixture((object)decks, fixture, null);
            Call(window, "PrepareCue", new Cue { Kind = CueKind.PowerPoint, FilePath = fixture, Title = "Moderador de prueba" }, null);
            Call(window, "ProjectPreparedWithoutActivation");
            Until(() => Field<PowerPointPresentation?>(window, "_powerPoint")?.IsRunning == true, "PowerPoint did not start");
            var presentation = Field<PowerPointPresentation>(window, "_powerPoint");
            Test("Moderator shows notes and next slide without opening another Office presentation", () =>
            {
                Check(open.IsEnabled, "Moderator cannot open during a live presentation");
                open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                moderator = Application.Current.Windows.OfType<Window>().Single(w => w.Title == "Vista de moderador");
                Check(moderator.Owner == window, "Moderator is not an operator-owned window");
                Check(((TextBlock)moderator.FindName("ModeratorNotes")).Text.Contains("Notas privadas de la primera diapositiva"),
                    "Speaker notes are missing");
                Check(((Image)moderator.FindName("ModeratorNextImage")).Source is not null, "Next slide thumbnail is missing");
                Check((int)decks.Count == count + 1, "Moderator opened a second native presentation");
                var monitor = (OutputMonitor)moderator.FindName("ModeratorMonitor");
                Until(() => monitor.IsConnected, "Moderator does not mirror the live native slide");
                moderator.Activate();
                WaitForPowerPointMonitorLayout(monitor);
                PumpFor(200);
                var baseline = CapturePowerPointMonitorPixels(monitor, Path.Combine(directory, "moderator-before.png"));
                ((Button)moderator.FindName("ModeratorNextButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => presentation.ClickIndex == 1, "Moderator Next did not advance the build");
                AssertPowerPointMonitorChanges(monitor, baseline, Path.Combine(directory, "moderator-after.png"),
                    .01, 1.0, "Moderator is connected but its visible slide does not change");
                ((Button)moderator.FindName("ModeratorPreviousButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => presentation.ClickIndex == 0, "Moderator Previous did not undo the build");
                PumpFor(200);
                CaptureDesignWindow(moderator, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-moderator-first.png"));
                var origin = moderator.PointToScreen(new Point());
                CaptureService.CaptureDisplay(new DisplayInfo("moderator", "Moderator window", (int)origin.X, (int)origin.Y,
                    (int)moderator.ActualWidth, (int)moderator.ActualHeight, false),
                    Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-moderator-physical.png"));
                open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Application.Current.Windows.OfType<Window>().Count(w => w.Title == "Vista de moderador") == 1,
                    "Opening moderator twice created duplicate windows");
            });
            if (moderator is null) return;
            Test("Moderator renders the native slide at its own pixel size instead of enlarging the console thumbnail", () =>
            {
                var monitor = (OutputMonitor)moderator.FindName("ModeratorMonitor");
                var start = monitor.PointToScreen(new Point());
                var end = monitor.PointToScreen(new Point(monitor.ActualWidth, monitor.ActualHeight));
                Check(PowerPointTestGetClientRect(presentation.Handle, out var rendered), "Cannot measure the native render surface");
                Console.WriteLine($"Moderator render: native={rendered.Right}x{rendered.Bottom}; viewport={end.X - start.X:F0}x{end.Y - start.Y:F0}");
                Check(rendered.Right >= Math.Round(end.X - start.X) - 1 && rendered.Bottom >= Math.Round(end.Y - start.Y) - 1,
                    "The native slide is smaller than the moderator viewport and is being enlarged");
                Check(PowerPointTestGetParent(presentation.Handle) == new System.Windows.Interop.WindowInteropHelper(moderator).Handle,
                    "Moderator still depends on the small console's rasterized slide");
            });
            Test("Moderator resize and minimize keep native rendering sharp and preserve playback", () =>
            {
                var monitor = (OutputMonitor)moderator.FindName("ModeratorMonitor");
                var slide = presentation.SlideIndex;
                var click = presentation.ClickIndex;
                moderator.WindowState = WindowState.Maximized;
                WaitForPowerPointMonitorLayout(monitor);
                var start = monitor.PointToScreen(new Point());
                var end = monitor.PointToScreen(new Point(monitor.ActualWidth, monitor.ActualHeight));
                Check(PowerPointTestGetClientRect(presentation.Handle, out var rendered)
                    && rendered.Right >= Math.Round(end.X - start.X) - 1 && rendered.Bottom >= Math.Round(end.Y - start.Y) - 1,
                    "Maximizing moderator enlarged a smaller native image");
                moderator.WindowState = WindowState.Minimized;
                Check(presentation.IsRunning && PowerPointTestGetParent(presentation.Handle) == new System.Windows.Interop.WindowInteropHelper(window).Handle,
                    "Minimizing moderator lost the native slideshow or left it in the minimized window");
                moderator.WindowState = WindowState.Normal;
                WaitForPowerPointMonitorLayout(monitor);
                Check(PowerPointTestGetParent(presentation.Handle) == new System.Windows.Interop.WindowInteropHelper(moderator).Handle,
                    "Restoring moderator did not restore native rendering");
                Check(presentation.SlideIndex == slide && presentation.ClickIndex == click, "Resizing restarted the slide or its animations");
            });
            Test("Moderator remains usable at compact size and large note text", () =>
            {
                moderator.Width = 900; moderator.Height = 600;
                var notes = (TextBlock)moderator.FindName("ModeratorNotes");
                notes.FontSize = 36;
                var originalNotes = notes.Text;
                notes.Text = string.Join("\n", Enumerable.Repeat("Notas largas del presentador que solo ve el moderador.", 12));
                moderator.UpdateLayout(); PumpFor(150);
                var viewport = new Rect(moderator.RenderSize);
                foreach (var name in new[] { "ModeratorNextButton", "ModeratorPreviousButton", "ModeratorPauseButton", "ModeratorRestartButton",
                    "ModeratorBlackoutButton", "ModeratorNotesScroll", "ModeratorNextImage" })
                    CheckWithin((FrameworkElement)moderator.FindName(name), moderator, viewport, name);
                var previous = (Button)moderator.FindName("ModeratorNextButton");
                var pause = (Button)moderator.FindName("ModeratorPauseButton");
                Check(!BoundsWithin(previous, moderator).IntersectsWith(BoundsWithin(pause, moderator)), "Compact moderator controls overlap");
                CaptureDesignWindow(moderator, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-moderator-compact.png"));
                notes.Text = originalNotes; notes.FontSize = 18;
                moderator.Width = 1120; moderator.Height = 740;
            });
            Test("Moderator controls preserve builds and update notes at the last slide", () =>
            {
                void Click(string name) => ((Button)moderator.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Click("ModeratorNextButton");
                Until(() => presentation.ClickIndex == 1, "Moderator Next skipped the animation build");
                Check(presentation.SlideIndex == 1, "Moderator Next skipped the first slide");
                Click("ModeratorNextButton");
                Until(() => presentation.SlideIndex == 2, "Moderator Next did not reach slide two");
                Until(() => ((TextBlock)moderator.FindName("ModeratorPosition")).Text == "Diapositiva 2 de 2", "Moderator counter is stale");
                Check(((Image)moderator.FindName("ModeratorNextImage")).Source is null, "The last slide still shows an old next slide");
                Check(((TextBlock)moderator.FindName("ModeratorNotes")).Text == "Sin notas para esta diapositiva.", "Previous notes leaked into the last slide");
                Click("ModeratorBlackoutButton");
                Check(Field<PresentationSession>(window, "_session").IsBlackout, "Moderator blackout did not affect the live output");
                Check(!((Button)moderator.FindName("ModeratorNextButton")).IsEnabled, "Blackout leaves moderator navigation enabled");
                Click("ModeratorBlackoutButton");
                Check(!Field<PresentationSession>(window, "_session").IsBlackout, "Moderator could not restore the live output");
                Until(() => ((OutputMonitor)moderator.FindName("ModeratorMonitor")).IsConnected, "Moderator did not restore its live image after blackout");
                PumpFor(200);
                CaptureDesignWindow(moderator, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-moderator.png"));
            });
            Test("Closing moderator keeps the current presentation running", () =>
            {
                moderator.Close();
                Check(presentation.IsRunning && presentation.SlideIndex == 2, "Closing moderator stopped or restarted the presentation");
                Check(PowerPointTestGetParent(presentation.Handle) == new System.Windows.Interop.WindowInteropHelper(window).Handle,
                    "Closing moderator did not return the native slide to the console");
                open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                moderator = Application.Current.Windows.OfType<Window>().Single(w => w.Title == "Vista de moderador");
                Call(window, "CloseOutput");
                Check(((TextBlock)moderator.FindName("ModeratorNotes")).Text == "Inicia una presentación para ver sus notas.", "Stopped presentation left private notes on screen");
                Check(!((Button)moderator.FindName("ModeratorNextButton")).IsEnabled, "Stopped moderator can advance unrelated content");
                Call(window, "PrepareCue", new Cue { Kind = CueKind.PowerPoint, FilePath = fixture, Title = "Otra presentación" }, null);
                Call(window, "ProjectPreparedWithoutActivation");
                Until(() => Field<PowerPointPresentation?>(window, "_powerPoint")?.IsRunning == true, "Moderator prevented a new presentation from starting");
                Until(() => ((TextBlock)moderator.FindName("ModeratorNotes")).Text.Contains("Notas privadas de la primera diapositiva"),
                    "Moderator did not replace stopped presentation notes with the new slide");
            });
        }
        finally
        {
            moderator?.Close();
            Call(window, "CloseOutput");
            Test("Moderator preserves pre-existing Office documents", () => Check((int)decks.Count == count, "Moderator retained or closed another deck"));
            ReleasePowerPointTestCom((object)decks);
            ReleasePowerPointTestCom((object)office);
        }
    }
}
