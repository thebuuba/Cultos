using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;
using Culto.Desktop.Dialogs;

internal static partial class Program
{
    private static void RunBackdropControlsTests(MainWindow window)
    {
        var session = Field<PresentationSession>(window, "_session");
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var oldBackground = workspace.BackgroundImagePath;
        var oldImages = workspace.BackgroundImages.ToList();
        var oldLogoOptions = workspace.LogoPlayback;
        var volume = (Slider)window.FindName("VolumeSlider");
        var oldVolume = volume.Value;
        var imagePath = Path.Combine(Path.GetTempPath(), "CultoChurchBackground-" + Guid.NewGuid().ToString("N") + ".png");
        var secondImagePath = Path.Combine(Path.GetTempPath(), "CultoChurchBackground-" + Guid.NewGuid().ToString("N") + ".png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { 20, 180, 30, 255 }, 4)));
        using (var stream = File.Create(imagePath)) encoder.Save(stream);
        using (var stream = File.Create(secondImagePath)) encoder.Save(stream);
        void Click(string name)
        {
            var button = window.FindName(name) as Button;
            Check(button is not null, "Backdrop control is missing: " + name);
            button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        ProjectionSurface StartVideo()
        {
            Call(window, "CloseOutput");
            Call(window, "PrepareCue", new Cue { Kind = CueKind.Video, Title = "Video para fondos",
                FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
            Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            var states = new List<string>();
            try
            {
                Until(() =>
                {
                    var player = Field<LibVLCSharp.Shared.MediaPlayer?>(output, "_player");
                    var overlay = Field<Image>(output, "_dissolve");
                    var state = $"native={player?.State},position={output.Position:0.0},paused={output.IsPaused},transition={output.IsTransitioning},waitingFrame={Field<bool>(output, "_waitingForFrame")},opacity={overlay.Opacity:0.00},black={session.IsBlackout},hidden={session.IsTextHidden}";
                    if (states.LastOrDefault() != state) states.Add(state);
                    return output.IsPlaying && output.Position > .2 && !output.IsTransitioning;
                }, "Backdrop fixture did not begin local playback");
            }
            catch (Exception error) { throw new InvalidOperationException(error.Message + ": " + string.Join("; ", states.TakeLast(25)), error); }
            return output;
        }
        try
        {
            volume.Value = 0;
            Test("Black backdrop button preserves live and prepared cues and toggles back to paused content", () =>
            {
                var output = StartVideo();
                Call(window, "PrepareCue", new Cue { Kind = CueKind.Video, Title = "Previa que se conserva",
                    FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
                var preview = Field<ProjectionSurface>(window, "_preview");
                Check(!preview.IsPlaying && preview.Duration == 0, "Preparing video opened another player");
                var live = session.Live; var prepared = session.Prepared;
                var media = Field<object>(output, "_media");
                Click("BlackoutButton");
                Until(() => !output.IsTransitioning && !output.IsPlaying, "Black backdrop did not finish and pause output");
                var position = output.Position;
                Check(session.IsBlackout && !session.IsTextHidden, "Black backdrop did not select its exclusive session state");
                Check(ReferenceEquals(session.Live, live) && ReferenceEquals(session.Prepared, prepared), "Backdrop selection replaced live or prepared content");
                Check(!preview.IsPlaying && preview.Duration == 0
                    && ((FrameworkElement)window.FindName("PreparedMediaCover")).Visibility == Visibility.Visible,
                    "Backdrop selection replaced the prepared cover or started preview playback");
                Check(Field<ProjectionWindow?>(window, "_outputWindow") is null, "Local backdrop unnecessarily opened an HDMI window");
                Click("BlackoutButton");
                Until(() => !output.IsTransitioning, "Restoring content from black did not finish");
                Check(!session.IsBlackout && !session.IsTextHidden && !output.IsPlaying && output.IsPaused,
                    "Toggling the active black backdrop failed to restore paused content");
                Check(ReferenceEquals(media, Field<object>(output, "_media")) && Math.Abs(output.Position - position) < .2,
                    "Restoring from black reloaded or restarted the retained video");
            });

            Test("Church backdrop uses the saved image and switches exclusively with black and paused content", () =>
            {
                Call(window, "SetChurchBackground", imagePath);
                Check(workspace.BackgroundImagePath == imagePath, "Church background selection did not update the workspace");
                Check(new WorkspaceStore().Load().BackgroundImagePath == imagePath, "Saved workspace lost the church background path");
                Check(new WorkspaceStore().Load().BackgroundImages.Contains(imagePath), "Saved gallery lost its image list");
                var output = StartVideo();
                Call(window, "PrepareCue", new Cue { Title = "Texto preparado independiente", Body = "Conservar esta previa" }, null);
                var live = session.Live; var prepared = session.Prepared;
                Click("ChurchBackgroundButton");
                Check(!Application.Current.Windows.OfType<LogoSettingsDialog>().Any(), "Left-click opened the Logo editor instead of projecting");
                Check(Field<LogoSlideshow>(window, "_logoSlides").IsRunning && Field<DispatcherTimer>(window, "_logoTimer").IsEnabled,
                    "Logo did not start its slideshow");
                Until(() => !output.IsTransitioning && !output.IsPlaying, "Church backdrop did not finish and pause output");
                Check(session.IsTextHidden && !session.IsBlackout, "Church backdrop did not select its exclusive session state");
                Check(typeof(ProjectionSurface).GetProperty("BackgroundImagePath")?.GetValue(output) as string == imagePath,
                    "Opening local output did not apply the saved church background image");
                Click("BlackoutButton");
                Until(() => !output.IsTransitioning, "Switching church backdrop to black did not finish");
                Check(session.IsBlackout && !session.IsTextHidden, "Switching to black left the church backdrop active");
                Check(!Field<LogoSlideshow>(window, "_logoSlides").IsRunning && !Field<DispatcherTimer>(window, "_logoTimer").IsEnabled,
                    "Black backdrop left the Logo slideshow running");
                Click("ChurchBackgroundButton");
                Until(() => !output.IsTransitioning, "Switching black backdrop to church did not finish");
                Check(session.IsTextHidden && !session.IsBlackout, "Switching to church left black active");
                Call(window, "RestoreLogoContentClick", window, new RoutedEventArgs());
                Until(() => !output.IsTransitioning, "Restoring content from church did not finish");
                Check(!session.IsTextHidden && !session.IsBlackout && output.IsPaused && !output.IsPlaying,
                    "Toggling active church backdrop restarted playback or retained suppression");
                Check(ReferenceEquals(session.Live, live) && ReferenceEquals(session.Prepared, prepared),
                    "Backdrop switching changed the retained live or prepared cue");
            });

            Test("Logo cycles without changing the saved starting image and stops when a new cue projects", () =>
            {
                workspace.BackgroundImages = [imagePath, secondImagePath];
                workspace.BackgroundImagePath = imagePath;
                workspace.LogoPlayback = new LogoPlaybackOptions { IntervalSeconds = 1, TransitionMilliseconds = 100 };
                Click("ChurchBackgroundButton");
                var output = Field<ProjectionSurface>(window, "_output");
                Until(() => output.BackgroundImagePath == secondImagePath && !output.IsTransitioning,
                    "Logo did not advance to the second image");
                Check(workspace.BackgroundImagePath == imagePath, "Automatic advance changed the saved starting image");
                Check(output.Transitions.DurationMilliseconds == 100 && output.Transitions.Kind == "Dissolve",
                    "Logo did not use its own transition duration");
                Call(window, "PrepareCue", new Cue { Title = "Después del logo", Body = "Contenido nuevo" }, null);
                Call(window, "ProjectPrepared");
                Until(() => session.Live?.Title == "Después del logo" && !output.IsTransitioning,
                    "New cue did not replace Logo");
                Check(!session.IsTextHidden && !session.IsBlackout && !Field<LogoSlideshow>(window, "_logoSlides").IsRunning
                    && !Field<DispatcherTimer>(window, "_logoTimer").IsEnabled, "New cue left Logo scheduled");
                Call(window, "AdvanceLogo");
                Check(session.Live?.Title == "Después del logo" && !session.IsTextHidden, "A stale Logo tick replaced the new cue");
            });

            Test("Removing a Logo image and saving updates its list without deleting files or changing output", () =>
            {
                var live = session.Live; var prepared = session.Prepared;
                var output = Field<ProjectionSurface>(window, "_output");
                Exception? editFailure = null;
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    var editor = Application.Current.Windows.OfType<LogoSettingsDialog>().Single();
                    try
                    {
                        var list = Field<ListBox>(editor, "_gallery");
                        list.SelectedIndex = editor.Images.IndexOf(imagePath);
                        Descendants(editor).OfType<Button>().Single(b => b.Name == "RemoveLogoImage").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Descendants(editor).OfType<Button>().Single(b => b.Name == "SaveLogoSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception error) { editFailure = error; editor.DialogResult = false; }
                }));
                Call(window, "LogoSettingsClick", window, new RoutedEventArgs());
                if (editFailure is not null) throw editFailure;
                var saved = new WorkspaceStore().Load();
                Check(!workspace.BackgroundImages.Contains(imagePath) && !saved.BackgroundImages.Contains(imagePath)
                    && saved.BackgroundImagePath == secondImagePath && File.Exists(imagePath), "Removing a tile deleted the file or retained it in saved gallery");
                Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared)
                    && ReferenceEquals(output, Field<ProjectionSurface>(window, "_output")), "Editing gallery changed the presentation");
            });
            RunLogoEditorTests(window, imagePath, secondImagePath);
        }
        finally
        {
            Call(window, "CloseOutput");
            workspace.BackgroundImagePath = oldBackground;
            workspace.BackgroundImages = oldImages;
            workspace.LogoPlayback = oldLogoOptions;
            volume.Value = oldVolume;
            Call(window, "SaveWorkspace");
            File.Delete(imagePath);
            File.Delete(secondImagePath);
        }
    }
}
