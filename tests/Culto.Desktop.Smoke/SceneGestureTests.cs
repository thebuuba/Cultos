using System;
using System.Linq;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Desktop.Projection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunSceneGestureTests(MainWindow window)
    {
        var session = Field<PresentationSession>(window, "_session");
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var initial = workspace.SceneTiles.ToList();
        var scene = new SceneTile { Id = "gesture-scene", Action = "cue", Label = "Gesture test",
            Cue = new Cue { Kind = CueKind.Text, Title = "Prepared scene", Body = "Preview first" } };
        var panel = (Panel)window.FindName("OutputBackdropsPanel");
        Button Tile(string id) => panel.Children.OfType<Button>().Single(button => button.Tag as string == id);
        void DoubleClick(Button button)
        {
            var press = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent };
            typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))!.SetValue(press, 2);
            button.RaiseEvent(press);
            Check(press.Handled, "Second press was not consumed before the button could generate another click");
        }
        try
        {
            Call(window, "SaveSceneTile", scene);
            ((Slider)window.FindName("VolumeSlider")).Value = 0;
            Test("A single scene click only prepares and a double click projects", () =>
            {
                var live = new Cue { Title = "Current program", Body = "Keep on air" };
                Call(window, "PrepareCue", live, null); Call(window, "ProjectPrepared");
                var button = Tile(scene.Id);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(session.Live?.Id == live.Id && session.Prepared?.Id == scene.Cue!.Id,
                    "Single scene click changed program instead of preparing preview");
                DoubleClick(button);
                Check(session.Live?.Id == scene.Cue!.Id, "Double click did not project the scene");
                Check(!Field<bool>(window, "_independentScenePreview"), "Double click left an independent preview active");
            });
            Test("Project button uses the prepared scene including built-in blackout semantics", () =>
            {
                var live = session.Live?.Id;
                Tile("blackout").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(session.Live?.Id == live && !session.IsBlackout, "Single blackout click changed program");
                Call(window, "ProjectPreparedFromInput");
                Check(session.IsBlackout, "Project button treated blackout as a generic empty scene");
                Tile(scene.Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Call(window, "ProjectPreparedFromInput");
                Check(!session.IsBlackout && session.Live?.Id == scene.Cue!.Id, "Project button did not send the custom prepared scene");
            });
            Test("Selecting other content clears the prepared scene action", () =>
            {
                Tile("blackout").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var other = new Cue { Title = "Other selection", Body = "Regular content" };
                Call(window, "PrepareCue", other, null); Call(window, "ProjectPreparedFromInput");
                Check(!session.IsBlackout && session.Live?.Id == other.Id, "Project kept a stale scene action after selecting other content");
            });
            Test("Navigation that replaces prepared content cannot dispatch an old scene action", () =>
            {
                Tile("blackout").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var other = new Cue { Title = "Navigated content", Body = "Next item" };
                session.Prepare(other);
                Call(window, "ProjectPreparedFromInput");
                Check(!session.IsBlackout && session.Live?.Id == other.Id, "Navigation left an old blackout action armed");
            });
            var store = Field<WorkspaceStore>(window, "_store");
            string Logo(string name, byte red, byte blue)
            {
                var path = Path.Combine(store.DirectoryPath, name);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null,
                    new byte[] { blue, 0, red, 255 }, 4)));
                using var file = File.Create(path); encoder.Save(file);
                return path;
            }
            var firstLogo = Logo("first-logo.png", 255, 0);
            var selectedLogo = Logo("selected-logo.png", 0, 255);
            workspace.BackgroundImages = [firstLogo, selectedLogo]; workspace.BackgroundImagePath = selectedLogo;
            workspace.LogoPlayback = new LogoPlaybackOptions { IntervalSeconds = 1, TransitionMilliseconds = 100 };
            Test("Projecting Logo makes preview mirror the actual selected logo", () =>
            {
                Tile("logo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                DoubleClick(Tile("logo"));
                Check(session.IsTextHidden && session.PreviewFollowsLive, "Projected Logo left preview on an independent image");
                Check(((FrameworkElement)window.FindName("PreviewLiveMonitor")).Visibility == Visibility.Visible,
                    "Preview is not mirroring the live logo compositor");
            });
            Test("Selecting an already live Logo keeps both monitors synchronized through rotation", () =>
            {
                Call(window, "ShowLogo");
                Tile("logo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(session.PreviewFollowsLive && !Field<bool>(window, "_independentScenePreview"),
                    "Clicking the active Logo created a separate still preview");
                Call(window, "ProjectPreparedFromInput");
                Check(session.IsTextHidden && !session.IsBlackout, "Project restored old content instead of keeping the selected Logo");
                var monitor = (OutputMonitor)window.FindName("PreviewLiveMonitor");
                var output = Field<ProjectionSurface>(window, "_output");
                var brush = Field<VisualBrush>(monitor, "_localImage");
                Check(ReferenceEquals(brush.Visual, output), "Preview and program use different compositors");
                var path = Field<string>(window, "_logoCurrentPath");
                Until(() => Field<string>(window, "_logoCurrentPath") != path, "Logo rotation did not advance");
                Check(monitor.Visibility == Visibility.Visible && ReferenceEquals(Field<VisualBrush>(monitor, "_localImage").Visual, output),
                    "Logo rotation desynchronized the preview");
                CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "logo-synchronized.png"));
            });
            Test("A synchronized startup Logo remains projectable when no content was previously live", () =>
            {
                session.RestoreOutput(null, false, false);
                Call(window, "ShowLogo");
                Tile("logo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(session.Prepared is null && session.PreviewFollowsLive, "Empty startup content did not synchronize");
                Call(window, "ProjectPreparedFromInput");
                Check(session.IsTextHidden && session.PreviewFollowsLive, "Project lost the startup Logo scene action");
            });
            foreach (var input in new[] { "F5", "remote", "streamdeck", "ShiftF5", "remote-preserve" })
            {
                Test("Prepared scene is honored by " + input, () =>
                {
                    var live = new Cue { Title = "Input route " + input, Body = "Keep identity" };
                    Call(window, "PrepareCue", live, null); Call(window, "ProjectPrepared");
                    Tile("blackout").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (input is "F5" or "ShiftF5")
                        window.InputBindings.OfType<KeyBinding>().Single(binding => binding.Key == Key.F5
                            && binding.Modifiers == (input == "ShiftF5" ? ModifierKeys.Shift : ModifierKeys.None)).Command.Execute(null);
                    else if (input == "streamdeck") Call(window, "HandleStreamDeckAction", "project");
                    else Call(window, "ExecuteRemote", new Culto.Remote.RemoteCommand("project", Guid.NewGuid().ToString("N"), PreserveOther: input == "remote-preserve"));
                    Check(session.IsBlackout && session.Live?.Id == live.Id, "Input route replaced live content instead of applying blackout");
                    if (input is "ShiftF5" or "remote-preserve") Check(Field<bool>(window, "_preserveAudioForBackdrop"), "Preserve-audio intent was lost");
                    Check(!Field<bool>(window, "_handlingStreamDeck"), "Input route left activation suppression enabled");
                });
            }
            Test("Logo preview uses the configured starting image", () =>
            {
                Call(window, "PrepareCue", new Cue { Title = "Ordinary live", Body = "Text" }, null); Call(window, "ProjectPrepared");
                Tile("logo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(session.Prepared?.FilePath == selectedLogo, "Logo preview does not match its configured starting image");
            });
            Test("Remote Project keeps startup Logo mode when there is no previous live cue", () =>
            {
                session.RestoreOutput(null, false, false); Call(window, "ShowLogo");
                Tile("logo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var result = (Culto.Remote.CommandResult)Call(window, "ExecuteRemote", new Culto.Remote.RemoteCommand("project", Guid.NewGuid().ToString("N")))!;
                Check(result.Success && session.IsTextHidden && session.PreviewFollowsLive, "Remote Project rejected the prepared startup Logo");
            });
            Test("An asynchronous resolved cue commits without re-dispatching scene intent", () =>
            {
                Tile(scene.Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var resolved = scene.Cue!.Copy(); resolved.Body = "Resolved completion";
                session.Prepare(resolved);
                Call(window, "ProjectPreparedCore", true, false, false);
                Check(session.Live?.Body == resolved.Body, "Completion re-dispatched the original scene instead of committing resolved content");
            });
        }
        finally { Call(window, "CloseOutput"); Call(window, "SaveSceneTiles", initial); }
    }
}
