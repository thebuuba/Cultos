using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunCompactOrderTests(MainWindow window)
    {
        var plan = Field<ObservableCollection<Cue>>(window, "_plan");
        var list = (ListBox)window.FindName("PlanList");
        var session = Field<PresentationSession>(window, "_session");
        var preview = Field<ProjectionSurface>(window, "_preview");
        var video = new Cue { Title = "Video de prueba con un nombre largo para comprobar el espacio disponible", Kind = CueKind.Video,
            FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
        Call(window, "ActivateWorkspacePanel", "order");
        plan.Clear(); plan.Add(new Cue { Title = "Bienvenida", Body = "En vivo" }); plan.Add(video);
        list.SelectedIndex = 0; Call(window, "ProjectPrepared");
        var live = session.Live;
        Test("One selection autoplays a silent video preview without changing live output", () =>
        {
            list.SelectedItem = video;
            Until(() => preview.IsPlaying && preview.Position > 0, "Selected video did not autoplay in preview");
            Check(preview.Volume == 0, "Preview can produce audio");
            preview.Volume = 100;
            Check(Field<LibVLCSharp.Shared.MediaPlayer>(preview, "_player").Volume == 0,
                "Preview native audio can be unmuted");
            Check(((FrameworkElement)window.FindName("PreviewHost")).Visibility == Visibility.Visible, "Video preview is hidden");
            Check(ReferenceEquals(live, session.Live), "Video selection changed live output");
        });
        Test("Order rows are compact and include a real media thumbnail", () =>
        {
            list.ScrollIntoView(video); window.UpdateLayout();
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(video);
            Check(row.ActualHeight <= 42, $"Order row is too tall: {row.ActualHeight}");
            var thumbnail = Descendants(row).OfType<MediaThumbnail>().Single();
            Until(() => Field<System.Windows.Media.Imaging.BitmapSource?>(thumbnail, "_image") is not null
                && Field<TimeSpan?>(thumbnail, "_duration") > TimeSpan.Zero, "Video frame or duration did not load");
            Check(thumbnail.ShowDuration, "Order thumbnail does not show its duration");
            Call(window, "UpdatePlanCount");
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "compact-order.png"));
            var origin = window.PointToScreen(new Point());
            using var screenshot = new System.Drawing.Bitmap((int)window.ActualWidth, (int)window.ActualHeight);
            using (var graphics = System.Drawing.Graphics.FromImage(screenshot))
                graphics.CopyFromScreen((int)origin.X, (int)origin.Y, 0, 0, screenshot.Size);
            screenshot.Save(Path.Combine(Environment.CurrentDirectory, ".tools", "compact-order-screen.png"));
        });
        Test("Leaving video selection stops the preview decoder", () =>
        {
            list.SelectedIndex = 0;
            Until(() => !preview.IsPlaying, "Hidden video keeps playing after another selection");
        });
        Test("Sending video releases the preview and independently previewing another video preserves paused output", () =>
        {
            list.SelectedItem = video; Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            Until(() => output.IsPlaying && !output.IsTransitioning, "Video did not reach live output");
            Check(!preview.IsPlaying && preview.Duration == 0, "Following live retained a preview decoder");
            output.SetPaused(true);
            var position = output.Position;
            var otherVideo = video.Copy(newId: true); plan.Add(otherVideo); list.SelectedItem = otherVideo;
            Until(() => preview.IsPlaying, "Independent video did not preview while output was paused");
            Check(output.IsPaused && Math.Abs(output.Position - position) < .2 && session.Live?.Id == video.Id,
                "Previewing another video changed live playback");
        });
    }
}
