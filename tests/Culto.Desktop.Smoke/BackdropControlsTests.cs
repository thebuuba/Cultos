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
        var volume = (Slider)window.FindName("VolumeSlider");
        var oldVolume = volume.Value;
        var imagePath = Path.Combine(Path.GetTempPath(), "CultoChurchBackground-" + Guid.NewGuid().ToString("N") + ".png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { 20, 180, 30, 255 }, 4)));
        using (var stream = File.Create(imagePath)) encoder.Save(stream);
        void Click(string name, bool restore = false)
        {
            var button = window.FindName(name) as Button;
            Check(button is not null, "Backdrop control is missing: " + name);
            if (name == "ChurchBackgroundButton")
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    var gallery = Application.Current.Windows.OfType<BackgroundGalleryDialog>().Single();
                    var choice = Descendants(gallery).OfType<Button>().Single(b => restore
                        ? b.Content?.ToString() == "Volver al contenido" : b.Tag as string == imagePath);
                    choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
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
                Until(() => preview.IsPlaying, "Independent preview fixture did not begin playback");
                var live = session.Live; var prepared = session.Prepared;
                var media = Field<object>(output, "_media");
                Click("BlackoutButton");
                Until(() => !output.IsTransitioning && !output.IsPlaying, "Black backdrop did not finish and pause output");
                var position = output.Position;
                Check(session.IsBlackout && !session.IsTextHidden, "Black backdrop did not select its exclusive session state");
                Check(ReferenceEquals(session.Live, live) && ReferenceEquals(session.Prepared, prepared), "Backdrop selection replaced live or prepared content");
                Check(preview.IsPlaying && preview.Volume == 0 && ReferenceEquals(preview, Field<ProjectionSurface>(window, "_preview")),
                    "Backdrop selection changed the independent muted preview");
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
                Until(() => !output.IsTransitioning && !output.IsPlaying, "Church backdrop did not finish and pause output");
                Check(session.IsTextHidden && !session.IsBlackout, "Church backdrop did not select its exclusive session state");
                Check(typeof(ProjectionSurface).GetProperty("BackgroundImagePath")?.GetValue(output) as string == imagePath,
                    "Opening local output did not apply the saved church background image");
                Click("BlackoutButton");
                Until(() => !output.IsTransitioning, "Switching church backdrop to black did not finish");
                Check(session.IsBlackout && !session.IsTextHidden, "Switching to black left the church backdrop active");
                Click("ChurchBackgroundButton");
                Until(() => !output.IsTransitioning, "Switching black backdrop to church did not finish");
                Check(session.IsTextHidden && !session.IsBlackout, "Switching to church left black active");
                Click("ChurchBackgroundButton", restore: true);
                Until(() => !output.IsTransitioning, "Restoring content from church did not finish");
                Check(!session.IsTextHidden && !session.IsBlackout && output.IsPaused && !output.IsPlaying,
                    "Toggling active church backdrop restarted playback or retained suppression");
                Check(ReferenceEquals(session.Live, live) && ReferenceEquals(session.Prepared, prepared),
                    "Backdrop switching changed the retained live or prepared cue");
            });

            Test("Removing a gallery image saves the list without deleting the file or changing output", () =>
            {
                var live = session.Live; var prepared = session.Prepared;
                var output = Field<ProjectionSurface>(window, "_output");
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    var gallery = Application.Current.Windows.OfType<BackgroundGalleryDialog>().Single();
                    var tile = Descendants(gallery).OfType<Button>().Single(b => b.Tag as string == imagePath);
                    ((MenuItem)tile.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Descendants(gallery).OfType<Button>().Single(b => b.Name == "CloseGalleryButton")
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
                ((Button)window.FindName("ChurchBackgroundButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var saved = new WorkspaceStore().Load();
                Check(!workspace.BackgroundImages.Contains(imagePath) && !saved.BackgroundImages.Contains(imagePath)
                    && saved.BackgroundImagePath is null && File.Exists(imagePath), "Removing a tile deleted the file or retained it in saved gallery");
                Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared)
                    && ReferenceEquals(output, Field<ProjectionSurface>(window, "_output")), "Editing gallery changed the presentation");
            });
        }
        finally
        {
            Call(window, "CloseOutput");
            workspace.BackgroundImagePath = oldBackground;
            workspace.BackgroundImages = oldImages;
            volume.Value = oldVolume;
            Call(window, "SaveWorkspace");
            File.Delete(imagePath);
        }
    }
}
