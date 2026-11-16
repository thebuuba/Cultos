using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    // This suite creates and closes only its own temporary deck and service copies.
    // In particular, it never calls Application.Quit or enumerates/closes user decks.
    private static void RunPowerPointTests(MainWindow window, string directory)
    {
        Test("PowerPoint never projects the file name while entering or leaving", () =>
        {
            using var surface = new ProjectionSurface(smoothTransitions: true);
            surface.Transitions = new TransitionOptions { Kind = "Dissolve", DurationMilliseconds = 120,
                AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
            var cue = new Cue { Kind = CueKind.PowerPoint, Title = "PRIVATE FILE NAME", Body = "PRIVATE SLIDE NAME" };
            surface.ShowCue(cue);
            Check(Field<System.Windows.Controls.Viewbox>(surface, "_text").Visibility != Visibility.Visible,
                "The PowerPoint placeholder displays the file/slide name");
            surface.TransitionTo(new Cue { Kind = CueKind.Text, Body = "Next content" });
            Until(() => !surface.IsTransitioning, "Leaving PowerPoint did not finish");
            surface.TransitionTo(cue);
            Until(() => !surface.IsTransitioning, "Entering PowerPoint did not finish");
            Check(Field<System.Windows.Controls.Viewbox>(surface, "_text").Visibility != Visibility.Visible,
                "The PowerPoint transition displays the file/slide name");
        });
        if (!PowerPointPresentation.IsAvailable)
        {
            Console.WriteLine("SKIP native PowerPoint: Microsoft PowerPoint is not installed");
            return;
        }

        dynamic application = Activator.CreateInstance(Type.GetTypeFromProgID("PowerPoint.Application")!)!;
        dynamic presentations = application.Presentations;
        var originalCount = (int)presentations.Count;
        var fixture = Path.Combine(directory, "native-powerpoint-" + Guid.NewGuid().ToString("N") + ".pptx");
        var preview = Path.ChangeExtension(fixture, ".png");
        var optionalVideo = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mp4"),
            Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-test.mp4"),
            Path.Combine(Environment.CurrentDirectory, "tests", "Culto.Renderer.Smoke", "fixtures", "test.mp4")
        }.FirstOrDefault(File.Exists);
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var originalDisplay = workspace.DisplayDevice;
        try
        {
            Test("PowerPoint fixture is saved without leaving a presentation open", () =>
            {
                CreatePowerPointFixture((object)presentations, fixture, optionalVideo);
                Check(File.Exists(fixture), "Fixture was not saved");
                Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, ".tools"));
                File.Copy(fixture, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-qa.pptx"), overwrite: true);
                Check((int)presentations.Count == originalCount, "Fixture creation left a presentation open");
                if (optionalVideo is not null)
                {
                    using var zip = ZipFile.OpenRead(fixture);
                    Check(zip.Entries.Any(entry => entry.FullName.StartsWith("ppt/media/", StringComparison.Ordinal)
                        && entry.FullName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)), "Video was linked instead of embedded");
                }
                else Console.WriteLine("SKIP embedded PowerPoint video: no test.mp4 fixture is available");
            });
            if (!File.Exists(fixture)) return;
            var sourceHash = PowerPointFixtureHash(fixture);

            Test("Native PowerPoint advances builds before slides and preserves the source file", () =>
            {
                nint ownedShow = 0;
                using (var presentation = new PowerPointPresentation())
                {
                    presentation.Open(fixture);
                    Check((int)((dynamic)Field<object>(presentation, "_presentation")).Windows.Count == 0,
                        "Opening the deck created a PowerPoint editor window");
                    Check(presentation.SlideCount == 2, "PowerPoint did not open both fixture slides");
                    Check((int)presentations.Count == originalCount + 1, "Expected exactly one owned presentation copy");
                    presentation.ExportPreview(preview);
                    Check(File.Exists(preview) && new FileInfo(preview).Length > 100, "Native preview was not exported");
                    var foreground = PowerPointTestForegroundWindow();
                    var inputTick = PowerPointTestInputTick();
                    presentation.Start(null); // Offscreen window; no external display is used.
                    ownedShow = presentation.Handle;
                    Until(() => presentation.IsRunning && presentation.SlideIndex == 1, "Native slide show did not start");
                    CheckPowerPointForeground(foreground, inputTick, "Starting PowerPoint changed foreground ownership");
                    var firstClick = presentation.ClickIndex;
                    Check(presentation.Advance(1), "First animation click was rejected");
                    Until(() => presentation.ClickIndex > firstClick, "Next did not advance the native animation build");
                    Check(presentation.SlideIndex == 1, "Next skipped the animation and changed slide");
                    Check(presentation.Advance(1), "Next slide was rejected after completing the build");
                    Until(() => presentation.SlideIndex == 2, "Next did not move to the second native slide");
                    if (optionalVideo is not null)
                    {
                        AssertPowerPointVideoPlayback(presentation, () => Check(presentation.Advance(1), "Video play click was rejected"));
                        Check(!presentation.Advance(1) && presentation.SlideIndex == 2, "End of deck closed the show or left its last slide");
                        Check(presentation.Advance(-1), "Previous did not undo the media play build");
                    }
                    else Check(!presentation.Advance(1) && presentation.SlideIndex == 2, "End of deck closed the show or left its last slide");
                    Check(presentation.Advance(-1), "Previous slide was rejected");
                    Until(() => presentation.SlideIndex == 1, "Previous did not return to the first native slide");
                    presentation.Restart();
                    Until(() => presentation.SlideIndex == 1, "Restart did not return to the first slide");
                    Check(presentation.IsRunning, "Navigation closed the native presentation");
                    if (optionalVideo is not null)
                    {
                        presentation.Advance(1); presentation.Advance(1); presentation.Advance(1);
                        Until(() => PowerPointVideoPosition(presentation) > 100, "Restart fixture video did not start");
                        presentation.SetPaused(true);
                        presentation.Restart();
                        Check(presentation.SlideIndex == 1, "Restart while paused did not return to slide one");
                        presentation.SetPaused(false);
                        presentation.Advance(1); presentation.Advance(1);
                        Until(() => presentation.SlideIndex == 2, "Restart did not preserve native animation order");
                        var beforeMedia = PowerPointVideoPlayback(presentation);
                        PumpFor(200);
                        var afterMedia = PowerPointVideoPlayback(presentation);
                        Console.WriteLine($"RESTART MEDIA slide={presentation.SlideIndex} click={presentation.ClickIndex}: {beforeMedia}->{afterMedia}");
                        Check(presentation.ClickIndex == 0 && afterMedia.State != 0 && afterMedia.Position == beforeMedia.Position,
                            "Restart retained an old paused-media entry and resumed an unstarted build");
                    }
                }
                Check(!PowerPointTestIsWindowVisible(ownedShow), "Closing the owned show revealed a PowerPoint window");
                Check((int)presentations.Count == originalCount, "Disposal closed another presentation or retained its own copy");
                Check(PowerPointFixtureHash(fixture) == sourceHash, "Read-only playback changed the source PPTX");
            });

            Test("MainWindow projects and advances native PowerPoint without taking foreground focus", () =>
            {
                Call(window, "CloseOutput");
                workspace.DisplayDevice = null;
                var cue = new Cue { Title = "Native PowerPoint smoke", Kind = CueKind.PowerPoint, FilePath = fixture };
                var session = Field<PresentationSession>(window, "_session");
                Call(window, "PrepareCue", cue, null);
                var wasTopmost = window.Topmost;
                var previousWindowState = window.WindowState;
                window.Topmost = true;
                window.WindowState = WindowState.Maximized;
                window.UpdateLayout();
                PumpFor(200);
                var focusWindow = new Window
                {
                    Title = "PowerPoint smoke focus sentinel", ShowInTaskbar = false, Topmost = true,
                    Width = 160, Height = 80, Left = 20, Top = 20,
                    WindowStartupLocation = WindowStartupLocation.Manual
                };
                try
                {
                    focusWindow.Show();
                    focusWindow.Activate();
                    PumpFor(100);
                    var foreground = PowerPointTestForegroundWindow();
                    // Windows may refuse a background test's activation request.
                    var foregroundInputTick = PowerPointTestInputTick();
                    // An already focused third-party window is equally valid: the
                    // projection must preserve that exact foreground owner too.
                    Check(foreground != 0 && foreground != new System.Windows.Interop.WindowInteropHelper(window).Handle,
                        "Could not establish independent foreground ownership");
                    Call(window, "ProjectPreparedWithoutActivation");
                    Until(() => Field<PowerPointPresentation?>(window, "_powerPoint")?.IsRunning == true,
                        "MainWindow did not start the native PowerPoint output");
                    var presentation = Field<PowerPointPresentation>(window, "_powerPoint");
                    Check(session.Live?.Id == cue.Id && session.Live.Kind == CueKind.PowerPoint, "PowerPoint was not the live cue");
                    CheckPowerPointForeground(foreground, foregroundInputTick, "Background projection stole focus from another window");
                    Check(Field<object?>(window, "_outputWindow") is null, "Local PowerPoint opened an HDMI projection window");
                    var monitor = (OutputMonitor)window.FindName("LiveMonitor");
                    Until(() => monitor.IsConnected, "Live monitor did not connect to native PowerPoint output");
                    Check(Field<nint>(monitor, "_nativeSource") == presentation.Handle, "Live monitor is not displaying the native PowerPoint window");
                    Check(PowerPointTestGetParent(presentation.Handle) == new System.Windows.Interop.WindowInteropHelper(window).Handle,
                        "Local PowerPoint did not become a child of the live monitor's window");
                    var monitorTopLeft = monitor.PointToScreen(new Point());
                    var monitorBottomRight = monitor.PointToScreen(new Point(monitor.ActualWidth, monitor.ActualHeight));
                    var sentinelTopLeft = focusWindow.PointToScreen(new Point());
                    var sentinelBottomRight = focusWindow.PointToScreen(new Point(focusWindow.ActualWidth, focusWindow.ActualHeight));
                    Check(!new Rect(monitorTopLeft, monitorBottomRight).IntersectsWith(new Rect(sentinelTopLeft, sentinelBottomRight)),
                        "The focus sentinel overlaps the live monitor's pixel-test region");
                    WaitForPowerPointMonitorLayout(monitor);
                    Test("Native PowerPoint hides its status dock and fills its own client area", () =>
                        AssertPowerPointContentFillsWindow(presentation.Handle));
                    var beforeBuild = CapturePowerPointMonitorPixels(monitor, Path.Combine(directory, "native-monitor-before-build.png"));

                    var firstClick = presentation.ClickIndex;
                    var viewStateBefore = PowerPointViewState(presentation);
                    var usb = (System.Windows.Controls.CheckBox)window.FindName("PowerPointUsbCheck");
                    if (usb.IsChecked != true) usb.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.CheckBox.ClickEvent));
                    Check(Field<PresenterHotkeys>(window, "_presenterHotkeys").IsEnabled, "PowerPoint card did not enable USB control");
                    PresenterSendMessage(new System.Windows.Interop.WindowInteropHelper(window).Handle, 0x0312, 0x4351, 0x22 << 16);
                    var viewStateAfter = PowerPointViewState(presentation);
                    Until(() => presentation.ClickIndex > firstClick, "Presenter route did not advance the native build");
                    Check(presentation.SlideIndex == 1, "Presenter route skipped the native build");
                    PumpFor(50);
                    var sourceClass = new System.Text.StringBuilder(256);
                    PowerPointTestGetClassName(presentation.Handle, sourceClass, sourceClass.Capacity);
                    var colorMean = beforeBuild.Data.Where((_, index) => index % 4 != 3).Average(value => (double)value);
                    Console.WriteLine($"Native animation capture: slide={presentation.SlideIndex}, click={firstClick}->{presentation.ClickIndex}, "
                        + $"viewState={viewStateBefore}->{viewStateAfter}->{PowerPointViewState(presentation)}, class={sourceClass}, baselineRGB={colorMean:F2}");
                    Test("Physical desktop pixels show the native PowerPoint animation build", () =>
                        AssertPowerPointMonitorChanges(monitor, beforeBuild, Path.Combine(directory, "native-monitor-after-build.png"),
                            .01, 1.0, "The native build advanced but the visible live monitor stayed unchanged"));
                    Test("Physical desktop pixels undo the native PowerPoint animation build", () =>
                    {
                        var beforePrevious = CapturePowerPointMonitorPixels(monitor, Path.Combine(directory, "native-monitor-before-previous.png"));
                        ((System.Windows.Controls.Button)window.FindName("PowerPointPreviousButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        Until(() => presentation.ClickIndex == firstClick, "Previous did not undo the native build");
                        Check(presentation.SlideIndex == 1, "Previous skipped the build and changed slide");
                        AssertPowerPointMonitorChanges(monitor, beforePrevious, Path.Combine(directory, "native-monitor-after-previous.png"),
                            .01, 1.0, "The native build index moved backward but its visible image did not change");
                    });
                    Call(window, "HandlePresenterAdvance", 1);
                    Until(() => presentation.ClickIndex > firstClick, "Could not restore the native build after testing Previous");
                    ((System.Windows.Controls.Button)window.FindName("PowerPointNextButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    Until(() => presentation.SlideIndex == 2, "Next button did not advance the native slide");
                    Check(((System.Windows.Controls.TextBlock)window.FindName("PowerPointPositionText")).Text.StartsWith("Diapositiva 2 de "), "PowerPoint card does not show current slide");
                    if (optionalVideo is not null)
                        AssertPowerPointVideoPlayback(presentation, () => Call(window, "HandlePresenterAdvance", 1), () =>
                        {
                            Test("Physical desktop pixels show moving embedded PowerPoint video", () =>
                            {
                                var firstFrame = CapturePowerPointMonitorPixels(monitor, Path.Combine(directory, "native-monitor-video-first.png"));
                                PumpFor(350);
                                AssertPowerPointMonitorChanges(monitor, firstFrame, Path.Combine(directory, "native-monitor-video-next.png"),
                                    .001, .15, "The native video clock advanced but the visible live monitor image stayed frozen");
                            });
                        });
                    CheckPowerPointForeground(foreground, foregroundInputTick, "Native navigation stole foreground focus");

                    Call(window, "PrepareCue", new Cue { Title = "Independent preview", Body = "Preview only" }, null);
                    Check(session.Live?.Id == cue.Id && presentation.SlideIndex == 2 && presentation.IsRunning,
                        "Preparing another cue changed the live PowerPoint");
                    Call(window, "ToggleBlackout");
                    Call(window, "HandlePresenterAdvance", -1);
                    Check(presentation.SlideIndex == 2, "Presenter advanced a blacked-out presentation");
                    Until(() => PowerPointViewState(presentation) == 2 && presentation.AudioGain == 0
                        && !Field<ProjectionSurface>(window, "_output").IsTransitioning,
                        "Blackout did not finish its owned-media fade and pause the native presentation");
                    var blackoutPosition = 0;
                    if (optionalVideo is not null)
                    {
                        PumpFor(100);
                        blackoutPosition = PowerPointVideoPosition(presentation);
                        PumpFor(200);
                        Check(Math.Abs(PowerPointVideoPosition(presentation) - blackoutPosition) <= 50,
                            "Blackout left the embedded PowerPoint video playing");
                    }
                    Call(window, "ToggleBlackout");
                    if (optionalVideo is not null)
                    {
                        Call(window, "TogglePause");
                        Until(() => PowerPointVideoPosition(presentation) > blackoutPosition + 100,
                            "Continue did not resume embedded video after restoring from blackout");
                    }
                }
                finally
                {
                    Call(window, "CloseOutput");
                    focusWindow.Close();
                    window.Topmost = wasTopmost;
                    window.WindowState = previousWindowState;
                }
                Check((int)presentations.Count == originalCount, "MainWindow closed another deck or retained its owned deck");
                Check(PowerPointFixtureHash(fixture) == sourceHash, "MainWindow modified the source presentation");
            });
            Test("PowerPoint card projects selected file and owns navigation playback and USB controls", () =>
            {
                Call(window, "CloseOutput");
                try
                {
                    Call(window, "ActivateWorkspacePanel", "powerpoint");
                    Wait((Task)Call(window, "ImportMediaAsync", (object)new[] { fixture })!);
                    var files = (System.Windows.Controls.ListBox)window.FindName("PowerPointFiles");
                    files.SelectedItem = files.Items.OfType<Cue>().Single(c => c.FilePath == fixture);
                    Call(window, "ActivateWorkspacePanel", "powerpoint"); window.UpdateLayout();
                    void Click(string name) => ((System.Windows.Controls.Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    Click("PowerPointProjectButton");
                    Until(() => Field<PowerPointPresentation?>(window, "_powerPoint")?.IsRunning == true, "Card Project did not start selected file");
                    var presentation = Field<PowerPointPresentation>(window, "_powerPoint");
                    Click("PowerPointNextButton"); Until(() => presentation.ClickIndex > 0, "Card Next skipped animation");
                    Click("PowerPointPreviousButton"); Until(() => presentation.ClickIndex == 0, "Card Previous did not undo animation");
                    Click("PowerPointPauseButton"); Until(() => PowerPointViewState(presentation) == 2, "Card Pause failed");
                    Click("PowerPointPauseButton"); Until(() => PowerPointViewState(presentation) == 1, "Card Continue failed");
                    Click("PowerPointNextButton"); Until(() => presentation.ClickIndex > 0, "Card Next failed after pause");
                    Click("PowerPointNextButton"); Until(() => presentation.SlideIndex == 2, "Card did not reach second slide");
                    Click("PowerPointRestartButton"); Until(() => presentation.SlideIndex == 1 && presentation.ClickIndex == 0, "Card Restart failed");
                    Click("PowerPointNextButton"); Click("PowerPointNextButton");
                    Until(() => presentation.SlideIndex == 2, "Rapid Next presses were dropped during audio fade");
                    Click("PowerPointRestartButton"); Until(() => presentation.SlideIndex == 1 && presentation.ClickIndex == 0, "Restart after rapid advance failed");
                    var usb = (System.Windows.Controls.CheckBox)window.FindName("PowerPointUsbCheck");
                    if (usb.IsChecked != true) usb.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.CheckBox.ClickEvent));
                    ((System.Windows.Controls.ComboBox)window.FindName("PowerPointUsbKeys")).SelectedIndex = 1;
                    Check(workspace.PresenterUsesArrows && Field<PresenterHotkeys>(window, "_presenterHotkeys").IsEnabled, "Card USB arrow mode not applied");
                    var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    PresenterSendMessage(handle, 0x0312, 0x4351, 0x27 << 16);
                    Until(() => presentation.ClickIndex > 0, "USB Right did not advance live PowerPoint");
                    PresenterSendMessage(handle, 0x0312, 0x4352, 0x25 << 16);
                    Until(() => presentation.ClickIndex == 0, "USB Left did not undo live PowerPoint build");
                    presentation.Advance(1); presentation.Advance(1);
                    Until(() => presentation.SlideIndex == 2, "Native deck did not advance outside app controls");
                    Until(() => ((System.Windows.Controls.TextBlock)window.FindName("PowerPointPositionText")).Text.StartsWith("Diapositiva 2 de "),
                        "Card counter missed a native slide change outside app controls");
                    Check(((System.Windows.Controls.TextBlock)window.FindName("NextLiveText")).Text.StartsWith("Diapositiva 2 de "), "Monitor counter disagrees with the PowerPoint card");
                    CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-card-live.png"));
                    usb.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.CheckBox.ClickEvent));
                    Check(!Field<PresenterHotkeys>(window, "_presenterHotkeys").IsEnabled, "Card did not release USB keys");
                    ((System.Windows.Controls.ComboBox)window.FindName("PowerPointUsbKeys")).SelectedIndex = 0;
                }
                finally { Call(window, "CloseOutput"); }
                Check((int)presentations.Count == originalCount, "Card retained its owned presentation");
            });
            Test("PowerPoint pending startup cannot reveal content during blackout", () =>
            {
                Call(window, "CloseOutput");
                var cue = new Cue { Title = "Pending native presentation", Kind = CueKind.PowerPoint, FilePath = fixture };
                try
                {
                    Call(window, "PrepareCue", cue, null);
                    Call(window, "ProjectPreparedWithoutActivation");
                    Call(window, "ToggleBlackout");
                    var output = Field<ProjectionSurface>(window, "_output");
                    Until(() => !output.IsTransitioning, "Blackout did not finish");
                    PumpFor(100);
                    Check(Field<PowerPointPresentation?>(window, "_powerPoint") is null, "Blackout started a native slideshow");
                    Check(Field<PowerPointPresentation?>(window, "_pendingPowerPoint") is not null, "Blackout discarded the prepared presentation");
                    Call(window, "ToggleBlackout");
                    Until(() => Field<PowerPointPresentation?>(window, "_powerPoint")?.IsRunning == true, "Restoring did not start the pending presentation");
                }
                finally { Call(window, "CloseOutput"); }
                Check((int)presentations.Count == originalCount, "Pending presentation cleanup changed other presentations");
            });
        }
        finally
        {
            Call(window, "CloseOutput");
            workspace.DisplayDevice = originalDisplay;
            Test("Pre-existing PowerPoint presentation count is preserved", () =>
                Check((int)presentations.Count == originalCount, "The original open-presentation count changed"));
            ReleasePowerPointTestCom((object)presentations);
            ReleasePowerPointTestCom((object)application);
            // The smoke host removes its own temporary directory after shutdown.
        }
    }

    private static void CheckPowerPointForeground(nint expected, uint inputTick, string failure)
    {
        // A human can keep using this shared desktop while the native test runs.
        // The product deliberately gives any new input priority over restoring focus.
        // Its guarded restore is posted at DispatcherPriority.Background after COM.
        Until(() => PowerPointTestInputTick() != inputTick || PowerPointTestForegroundWindow() == expected,
            failure + $" (expected 0x{expected:X})");
        var currentTick = PowerPointTestInputTick();
        if (currentTick != inputTick)
        { Console.WriteLine($"INFO Foreground assertion interrupted by new input: {inputTick}->{currentTick}"); return; }
        Check(PowerPointTestForegroundWindow() == expected, failure);
    }

    private static void CreatePowerPointFixture(object presentationsObject, string path, string? optionalVideo)
    {
        dynamic presentations = presentationsObject;
        var owned = new List<object>();
        object Track(object value) { owned.Add(value); return value; }
        dynamic? fixture = null;
        try
        {
            fixture = Track((object)presentations.Add(0)); // msoFalse: no editing window.
            dynamic slides = Track((object)fixture.Slides);
            dynamic first = Track((object)slides.Add(1, 12)); // ppLayoutBlank
            dynamic second = Track((object)slides.Add(2, 12));
            dynamic notes = Track((object)first.NotesPage);
            dynamic notesShapes = Track((object)notes.Shapes);
            dynamic placeholders = Track((object)notesShapes.Placeholders);
            dynamic notesBody = Track((object)placeholders[2]);
            dynamic notesFrame = Track((object)notesBody.TextFrame);
            dynamic notesText = Track((object)notesFrame.TextRange);
            notesText.Text = "Notas privadas de la primera diapositiva";
            dynamic firstShapes = Track((object)first.Shapes);
            dynamic shape = Track((object)firstShapes.AddShape(1, 80f, 80f, 280f, 160f));
            shape.Name = "Smoke on-click rectangle";
            dynamic timeline = Track((object)first.TimeLine);
            dynamic sequence = Track((object)timeline.MainSequence);
            Track((object)sequence.AddEffect(shape, 1, 0, 1)); // Appear, no text grouping, on page click.
            dynamic secondShapes = Track((object)second.Shapes);
            if (optionalVideo is null)
                Track((object)secondShapes.AddShape(1, 80f, 80f, 440f, 240f));
            else
            {
                dynamic video = Track((object)secondShapes.AddMediaObject2(Path.GetFullPath(optionalVideo), 0, -1, 80f, 80f, 440f, 240f));
                video.Name = "Smoke embedded video";
                dynamic secondTimeline = Track((object)second.TimeLine);
                dynamic secondSequence = Track((object)secondTimeline.MainSequence);
                while ((int)secondSequence.Count > 0)
                {
                    dynamic defaultEffect = Track((object)secondSequence[1]);
                    defaultEffect.Delete();
                }
                // Explicit on-click playback makes advancing video part of the same native route as animations.
                Track((object)secondSequence.AddEffect(video, 83, 0, 1)); // msoAnimEffectMediaPlay
            }
            fixture.SaveAs(path, 24); // ppSaveAsOpenXMLPresentation
        }
        finally
        {
            try
            {
                if (fixture is not null) { fixture.Saved = -1; fixture.Close(); }
            }
            finally
            {
                for (var i = owned.Count - 1; i >= 0; i--) ReleasePowerPointTestCom(owned[i]);
            }
        }
    }

    private static string PowerPointFixtureHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed record PowerPointMonitorPixels(int X, int Y, int Width, int Height, byte[] Data, string Path);

    private static void WaitForPowerPointMonitorLayout(OutputMonitor monitor)
    {
        var previous = Rect.Empty;
        var stableSince = DateTime.UtcNow;
        Until(() =>
        {
            monitor.UpdateLayout();
            var current = new Rect(monitor.PointToScreen(new Point()),
                monitor.PointToScreen(new Point(monitor.ActualWidth, monitor.ActualHeight)));
            if (current != previous)
            {
                previous = current;
                stableSince = DateTime.UtcNow;
            }
            return current.Width >= 80 && current.Height >= 50 && DateTime.UtcNow - stableSince >= TimeSpan.FromMilliseconds(350);
        }, "The live monitor layout did not settle before the physical pixel capture");
        monitor.Refresh();
    }

    private static PowerPointMonitorPixels CapturePowerPointMonitorPixels(OutputMonitor monitor, string path)
    {
        Check(monitor.IsVisible && monitor.ActualWidth >= 80 && monitor.ActualHeight >= 50,
            "The live monitor is not large enough for a physical pixel regression test");
        var origin = monitor.PointToScreen(new Point());
        var corner = monitor.PointToScreen(new Point(monitor.ActualWidth, monitor.ActualHeight));
        // Inset the actual screen rectangle to exclude control borders. Passing that
        // rectangle to CaptureDisplay captures only these physical desktop pixels,
        // including native child windows and DWM content that WPF bitmap rendering omits.
        int left = (int)Math.Ceiling(origin.X) + 3, top = (int)Math.Ceiling(origin.Y) + 3;
        int width = (int)Math.Floor(corner.X) - left - 3, height = (int)Math.Floor(corner.Y) - top - 3;
        Check(DisplayService.GetDisplays().Any(display => left >= display.Left && top >= display.Top
            && left + width <= display.Left + display.Width && top + height <= display.Top + display.Height),
            $"The physical monitor capture region {left},{top} {width}x{height} is outside attached displays: "
                + string.Join("; ", DisplayService.GetDisplays().Select(d => $"{d.Left},{d.Top} {d.Width}x{d.Height}")));
        CaptureService.CaptureDisplay(new DisplayInfo("native-monitor-region", "Native PowerPoint monitor", left, top, width, height, false), path);
        using var stream = File.OpenRead(path);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var pixels = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var data = new byte[width * height * 4];
        pixels.CopyPixels(data, width * 4, 0);
        return new PowerPointMonitorPixels(left, top, width, height, data, path);
    }

    private static void AssertPowerPointMonitorChanges(OutputMonitor monitor, PowerPointMonitorPixels before,
        string path, double minimumChangedFraction, double minimumMeanDifference, string failure)
    {
        double changedFraction = 0, meanDifference = 0;
        try { Until(() =>
        {
            var after = CapturePowerPointMonitorPixels(monitor, path);
            Check(after.Width == before.Width && after.Height == before.Height,
                "Live monitor dimensions changed during the pixel comparison");
            long totalDifference = 0;
            int changed = 0;
            for (int index = 0; index < before.Data.Length; index += 4)
            {
                int difference = Math.Abs(after.Data[index] - before.Data[index])
                    + Math.Abs(after.Data[index + 1] - before.Data[index + 1])
                    + Math.Abs(after.Data[index + 2] - before.Data[index + 2]);
                totalDifference += difference;
                if (difference >= 45) changed++;
            }
            changedFraction = changed / (double)(before.Width * before.Height);
            meanDifference = totalDifference / (double)(before.Width * before.Height * 3);
            return changedFraction >= minimumChangedFraction && meanDifference >= minimumMeanDifference;
        }, failure + $" (capture: {System.IO.Path.GetFileName(path)})"); }
        finally
        {
            var artifacts = System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "ppt-pixels");
            Directory.CreateDirectory(artifacts);
            File.Copy(before.Path, System.IO.Path.Combine(artifacts, System.IO.Path.GetFileName(before.Path)), overwrite: true);
            if (File.Exists(path)) File.Copy(path, System.IO.Path.Combine(artifacts, System.IO.Path.GetFileName(path)), overwrite: true);
            Console.WriteLine($"Native monitor pixel delta: {changedFraction:P2} changed; mean RGB difference {meanDifference:F2}; region={before.X},{before.Y} {before.Width}x{before.Height}");
        }
    }

    private static void AssertPowerPointVideoPlayback(PowerPointPresentation presentation, Action clickPlay, Action? verifyVisibleVideo = null)
    {
        // The show reference belongs to the service. Release only property RCWs acquired here.
        dynamic show = Field<object>(presentation, "_show");
        var owned = new List<object>();
        object Track(object value) { owned.Add(value); return value; }
        try
        {
            dynamic view = Track((object)show.View);
            dynamic slide = Track((object)view.Slide);
            dynamic shapes = Track((object)slide.Shapes);
            dynamic video = Track((object)shapes["Smoke embedded video"]);
            Check((int)video.MediaType == 3, "The embedded shape is not native movie media"); // ppMediaTypeMovie
            dynamic player = Track((object)view.Player((int)video.Id));
            var position = (int)player.CurrentPosition;
            presentation.SetPaused(true);
            presentation.SetPaused(false);
            PumpFor(200);
            Check((int)player.CurrentPosition <= position + 50 && (int)player.State != 0,
                "Pause/resume started media whose play build had not run");
            clickPlay();
            Until(() => (int)player.CurrentPosition > position + 100, "Native embedded video did not play after the presenter click");
            Check(presentation.SlideIndex == 2, "Video playback advanced past its slide");
            verifyVisibleVideo?.Invoke();
            presentation.SetPaused(true);
            PumpFor(100); // Allow an already queued media frame to finish before measuring.
            var pausedPosition = (int)player.CurrentPosition;
            presentation.SetPaused(true); // Repeated blackout/pause must retain the resume list.
            PumpFor(200);
            Check(Math.Abs((int)player.CurrentPosition - pausedPosition) <= 50 && (int)player.State == 1,
                "Pausing the slide show did not pause its embedded video");
            presentation.SetPaused(false);
            Until(() => (int)player.CurrentPosition > pausedPosition + 100,
                "Resuming the slide show did not resume its previously playing video");
        }
        finally
        {
            for (var i = owned.Count - 1; i >= 0; i--) ReleasePowerPointTestCom(owned[i]);
        }
    }
    private static void ReleasePowerPointTestCom(object value)
    {
        if (Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }
    private static int PowerPointVideoPosition(PowerPointPresentation presentation) => PowerPointVideoPlayback(presentation).Position;

    private static (int Position, int State) PowerPointVideoPlayback(PowerPointPresentation presentation)
    {
        dynamic show = Field<object>(presentation, "_show");
        var owned = new List<object>();
        object Track(object value) { owned.Add(value); return value; }
        try
        {
            dynamic view = Track((object)show.View);
            dynamic slide = Track((object)view.Slide);
            dynamic shapes = Track((object)slide.Shapes);
            dynamic video = Track((object)shapes["Smoke embedded video"]);
            dynamic player = Track((object)view.Player((int)video.Id));
            return ((int)player.CurrentPosition, (int)player.State);
        }
        finally
        {
            for (var i = owned.Count - 1; i >= 0; i--) ReleasePowerPointTestCom(owned[i]);
        }
    }
    private static int PowerPointViewState(PowerPointPresentation presentation)
    {
        dynamic show = Field<object>(presentation, "_show");
        dynamic view = show.View;
        try { return (int)view.State; }
        finally { ReleasePowerPointTestCom((object)view); }
    }
    private static void AssertPowerPointContentFillsWindow(nint showHandle)
    {
        var dock = PowerPointTestFindWindowEx(showHandle, 0, "MsoCommandBarDock", "MsoDockBottom");
        var content = PowerPointTestFindWindowEx(showHandle, 0, "MDIClient", null);
        Check(dock != 0 && content != 0, "The owned native slideshow did not expose its status dock and content area");
        Check(!PowerPointTestIsWindowVisible(dock), "PowerPoint status chrome remains visible in the live output");
        Check(PowerPointTestGetClientRect(showHandle, out var client), "Could not read the native slideshow client bounds");
        var origin = new PowerPointTestNativePoint();
        Check(PowerPointTestClientToScreen(showHandle, ref origin), "Could not map native client bounds to the physical display");
        Check(PowerPointTestGetWindowRect(content, out var contentBounds), "Could not read the native slide content bounds");
        Check(contentBounds.Left == origin.X + client.Left && contentBounds.Top == origin.Y + client.Top
            && contentBounds.Right == origin.X + client.Right && contentBounds.Bottom == origin.Y + client.Bottom,
            $"Native content does not fill its own client area: content={contentBounds.Left},{contentBounds.Top}..{contentBounds.Right},{contentBounds.Bottom}; "
                + $"client={origin.X + client.Left},{origin.Y + client.Top}..{origin.X + client.Right},{origin.Y + client.Bottom}");
    }
    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern nint PowerPointTestForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetParent")]
    private static extern nint PowerPointTestGetParent(nint handle);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int PowerPointTestGetClassName(nint handle, System.Text.StringBuilder text, int count);
    [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode)]
    private static extern nint PowerPointTestFindWindowEx(nint parent, nint after, string type, string? title);
    [StructLayout(LayoutKind.Sequential)] private struct PowerPointTestWindowRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct PowerPointTestNativePoint { public int X, Y; }
    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    private static extern bool PowerPointTestGetWindowRect(nint handle, out PowerPointTestWindowRect rectangle);
    [DllImport("user32.dll", EntryPoint = "GetClientRect")]
    private static extern bool PowerPointTestGetClientRect(nint handle, out PowerPointTestWindowRect rectangle);
    [DllImport("user32.dll", EntryPoint = "ClientToScreen")]
    private static extern bool PowerPointTestClientToScreen(nint handle, ref PowerPointTestNativePoint point);
    [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
    private static extern bool PowerPointTestIsWindowVisible(nint handle);
    [StructLayout(LayoutKind.Sequential)] private struct PowerPointTestInputInfo { public uint Size, Tick; }
    [DllImport("user32.dll", EntryPoint = "GetLastInputInfo")]
    private static extern bool PowerPointTestGetLastInput(ref PowerPointTestInputInfo info);
    private static uint PowerPointTestInputTick()
    {
        var info = new PowerPointTestInputInfo { Size = (uint)Marshal.SizeOf<PowerPointTestInputInfo>() };
        return PowerPointTestGetLastInput(ref info) ? info.Tick : 0;
    }
}
