using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void SeedLogoStartupWorkspace(string directory, bool missing)
    {
        var logo = Path.Combine(directory, "startup-logo.png");
        if (!missing)
        {
            var pixels = Enumerable.Repeat((byte)180, 32 * 32 * 3).ToArray();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(32, 32, 96, 96, PixelFormats.Rgb24, null, pixels, 96)));
            using var file = File.Create(logo); encoder.Save(file);
        }
        new WorkspaceStore().Save(new WorkspaceData
        {
            BackgroundImagePath = logo, BackgroundImages = [logo],
            Plan = new ServicePlan { Name = "Culto guardado con himno primero", Items = [new Cue
            { Title = "Primer himno", Kind = CueKind.Video, FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }] }
        });
    }

    private static void RunLogoStartupTests(MainWindow window, bool missing)
    {
        var session = Field<PresentationSession>(window, "_session");
        var preview = Field<ProjectionSurface>(window, "_preview");
        Test("Startup does not select or play the first saved hymn", () =>
        {
            PumpFor(600);
            Check(((ListBox)window.FindName("PlanList")).SelectedItem is null && session.Prepared is null && session.Live is null,
                "Startup selected a saved hymn");
            Check(!preview.IsPlaying && preview.Duration == 0, "Startup opened a hymn preview decoder");
            Check(((ListBox)window.FindName("PlanList")).Items.Count == 1, "Startup lost the saved order");
        });
        Test("Startup opens a silent local logo without activating HDMI", () =>
        {
            var output = Field<ProjectionSurface?>(window, "_output");
            Check(output is not null && !output.IsPlaying && output.Duration == 0, "Startup has no silent local output");
            Check(Field<ProjectionWindow?>(window, "_outputWindow") is null, "Startup activated HDMI");
            if (missing)
                Check(session.IsBlackout && ((TextBlock)window.FindName("StatusText")).Text.Contains("Logo"), "Missing logo did not leave a safe backdrop and explanation");
            else
            {
                Check(session.IsTextHidden && !session.IsBlackout, "Startup is not in logo mode");
                Check(((TextBlock)window.FindName("LiveTitleText")).Text == "Logo"
                    && ((TextBlock)window.FindName("PreparedTitleText")).Text == "Logo", "Monitors do not identify the startup logo");
                Check(output!.BackgroundImagePath == Field<WorkspaceData>(window, "_workspace").BackgroundImagePath, "Startup did not use the saved logo");
                Until(() => !output.IsTransitioning, "Startup logo transition did not complete");
                var bitmap = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(output);
                var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(960, 540, 1, 1), pixel, 4, 0);
                Check(pixel[0] > 150 && pixel[1] > 150 && pixel[2] > 150, "Saved logo pixels did not reach output");
                CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "logo-startup.png"));
            }
        });
        Test("A deliberate selection still autoplays a silent preview after startup", () =>
        {
            ((ListBox)window.FindName("PlanList")).SelectedIndex = 0;
            Until(() => preview.IsPlaying, "Explicit selection no longer previews video");
            Check(preview.Volume == 0 && session.Live is null, "Preview selection changed startup output or made sound");
        });
    }
}
