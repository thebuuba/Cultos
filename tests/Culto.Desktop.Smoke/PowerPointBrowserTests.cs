using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunPowerPointBrowserNativeTests(MainWindow window, string directory)
    {
        if (!PowerPointPresentation.IsAvailable) { Console.WriteLine("SKIP PowerPoint thumbnails: Microsoft PowerPoint is not installed"); return; }
        var path = Path.Combine(directory, "01 La cena.pptx");
        dynamic application = Activator.CreateInstance(Type.GetTypeFromProgID("PowerPoint.Application")!)!;
        dynamic presentations = application.Presentations;
        var originalCount = (int)presentations.Count;
        try { CreatePowerPointFixture((object)presentations, path, null); }
        finally { ReleasePowerPointTestCom((object)presentations); ReleasePowerPointTestCom((object)application); }
        var secondPath = Path.Combine(directory, "El-Poder-de-una-Fe-verdadera.pptx");
        var thirdPath = Path.Combine(directory, "El-Poder-de-una-Fe-verdadera-con-ilustraciones.pptx");
        File.Copy(path, secondPath); File.Copy(path, thirdPath);
        var first = MediaLibrary.FromFile(path); var second = MediaLibrary.FromFile(secondPath); var third = MediaLibrary.FromFile(thirdPath);
        var originalHash = PowerPointFixtureHash(path);
        var workspace = Field<WorkspaceData>(window, "_workspace");
        workspace.Library.RemoveAll(c => c.Kind == CueKind.PowerPoint);
        workspace.Library.AddRange([first, second, third]);
        workspace.Transitions = new TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
        ((Slider)window.FindName("VolumeSlider")).Value = 0;
        var files = (ListBox)window.FindName("PowerPointFiles");
        var slides = (ListBox)window.FindName("PowerPointSlides");
        void Ready()
        {
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while ((slides.Items.Count != 2 || slides.Items.OfType<PowerPointSlidePreview>().Any(s => s.Image is null)) && DateTime.UtcNow < deadline) PumpFor(50);
            Check(slides.Items.Count == 2 && slides.Items.OfType<PowerPointSlidePreview>().All(s => s.Image is { IsFrozen: true }),
                "Native Office thumbnails did not finish: " + ((TextBlock)window.FindName("PowerPointSlideHint")).Text);
        }
        Test("PowerPoint browser shows real slide counts and clean file titles", () =>
        {
            Call(window, "PrepareCue", first, null); Ready();
            Check(files.Items.Count == 3, "Browser did not retain its three presentations");
            var metadata = PowerPointFileMetadata.Get(first);
            Check(metadata.Title == "La cena" && metadata.Slides.Count == 2 && metadata.Details.Contains("2 diapositivas"), "Wrong Open XML metadata");
            Check(Field<PresentationSession>(window, "_session").Live?.Id != first.Id, "Generating previews projected the file");
            Call(window, "AddToPlan", first);
            var planCopy = workspace.Plan.Items.Last(c => c.FilePath == path);
            Call(window, "PrepareCue", planCopy, null);
            Check(files.SelectedItem is Cue selected && selected.FilePath == path, "Preparing a plan copy lost the library selection");
        });
        Test("Thumbnail selection and preview navigation choose the starting slide without going live", () =>
        {
            Ready(); slides.SelectedIndex = 1;
            Check(Field<int>(window, "_powerPointCursor") == 2, "Choosing a thumbnail did not select its starting slide");
            Check(Field<PresentationSession>(window, "_session").Live?.Id != first.Id, "Choosing a starting slide changed the output");
            ((Button)window.FindName("PowerPointPreviousButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Field<int>(window, "_powerPointCursor") == 1, "Preview previous did not move to slide one");
            slides.SelectedIndex = 1;
        });
        Test("Proyectar starts at the chosen slide and live thumbnails jump through native Office", () =>
        {
            ((Button)window.FindName("PowerPointProjectButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(() => Field<PowerPointPresentation?>(window, "_powerPoint") is { IsRunning: true, SlideIndex: 2 }, "Projection did not start on the selected second slide");
            slides.SelectedIndex = 0;
            Until(() => Field<PowerPointPresentation?>(window, "_powerPoint")?.SlideIndex == 1, "Live thumbnail did not jump to slide one");
            ((Button)window.FindName("PowerPointNextButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(() => Field<PowerPointPresentation?>(window, "_powerPoint")?.ClickIndex > 0, "Next skipped the native animation build");
        });
        Test("Preparing another file navigates its preview without changing the live presentation", () =>
        {
            var presentation = Field<PowerPointPresentation>(window, "_powerPoint");
            var index = presentation.SlideIndex; var build = presentation.ClickIndex;
            Call(window, "PrepareCue", second, null); Ready();
            ((Button)window.FindName("PowerPointNextButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Field<int>(window, "_powerPointCursor") == 2 && presentation.SlideIndex == index && presentation.ClickIndex == build,
                "Preview navigation modified the live presentation");
            ((Button)window.FindName("PowerPointRestartButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Field<int>(window, "_powerPointCursor") == 1 && presentation.SlideIndex == index && presentation.ClickIndex == build,
                "Preview restart modified the live presentation");
            Call(window, "PrepareCue", first, null); Ready();
        });
        Test("PowerPoint blackout preserves the live slide and Detener clears the output", () =>
        {
            var presentation = Field<PowerPointPresentation>(window, "_powerPoint");
            var index = presentation.SlideIndex;
            ((Button)window.FindName("PowerPointBlackoutButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Field<PresentationSession>(window, "_session").IsBlackout, "PowerPoint blackout was not applied");
            ((Button)window.FindName("PowerPointBlackoutButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!Field<PresentationSession>(window, "_session").IsBlackout && presentation.SlideIndex == index, "Restoring blackout restarted the slide");
            ((Button)window.FindName("PowerPointProjectButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Field<PresentationSession>(window, "_session").Live is null && Field<PowerPointPresentation?>(window, "_powerPoint") is null, "Detener left the presentation playing");
        });
        Test("PowerPoint library removal preserves the original file and the worship order", () =>
        {
            Call(window, "AddToPlan", first);
            Call(window, "RemovePowerPointFileClick", new MenuItem { Tag = first }, new RoutedEventArgs());
            Check(files.Items.Count == 2 && !workspace.Library.Any(c => c.FilePath == path), "Removed file returned to the browser");
            Check(workspace.Plan.Items.Any(c => c.FilePath == path) && File.Exists(path) && PowerPointFixtureHash(path) == originalHash,
                "Removing the library reference changed the original file or worship order");
            Call(window, "PrepareCue", second, null); Ready();
            window.Width = 1480; window.Height = 900; window.UpdateLayout(); PumpFor(100);
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-browser-native.png"));
        });
        Test("A missing presentation clears the previous file's thumbnails", () =>
        {
            var missing = new Cue { Kind = CueKind.PowerPoint, Title = "Missing", FilePath = Path.Combine(directory, "missing.pptx") };
            Call(window, "PrepareCue", missing, null);
            Check(slides.Items.Count == 0, "Missing file retained another presentation's thumbnails");
        });
        Call(window, "CloseOutput");
    }
}
