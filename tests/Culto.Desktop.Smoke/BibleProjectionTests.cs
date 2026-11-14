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
    private static void RunBibleProjectionTests(MainWindow window)
    {
        var session = Field<PresentationSession>(window, "_session");
        var workspace = Field<WorkspaceData>(window, "_workspace");
        Call(window, "SelectContentSource", 1);
        Call(window, "InstallBibleCatalog", new BibleCatalog(new BibleDocument
        {
            Name = "Prueba", Attribution = "Prueba",
            Verses = [new("Juan", 1, 1, "Primer versículo de prueba"), new("Juan", 1, 2, "Segundo versículo de prueba"),
                new("Hebreos", 5, 1, "Versículo para búsqueda tolerante")]
        }));
        workspace.BibleStyle = new BibleVisualStyle { Transition = "Dissolve", TransitionMilliseconds = 400,
            BackgroundImagePath = Environment.GetEnvironmentVariable("CULTO_BIBLE_TEST_BACKGROUND") };
        var results = (ListBox)window.FindName("BibleResults");
        results.SelectedIndex = 0;
        Call(window, "ProjectBibleResult", results.SelectedItem);
        var output = Field<ProjectionSurface>(window, "_output");
        Until(() => !output.IsTransitioning, "Initial verse did not finish transitioning");
        var selectionClock = System.Diagnostics.Stopwatch.StartNew();
        results.SelectedIndex = 1; // First click prepares the next verse.
        Console.WriteLine($"Bible selection: {selectionClock.Elapsed.TotalMilliseconds:F1} ms");
        window.UpdateLayout();

        Test("Bible double click sends the prepared selection without preparing it again", () =>
        {
            var prepares = 0;
            void Changed() { if (!session.PreviewFollowsLive) prepares++; }
            session.Changed += Changed;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try { Call(window, "ProjectBibleResult", results.SelectedItem); }
            finally { session.Changed -= Changed; }
            Console.WriteLine($"Bible send: {clock.Elapsed.TotalMilliseconds:F1} ms");
            Check(prepares == 0, $"Double click prepared the same verse again ({prepares})");
            Check(session.Live?.Reference == "Juan 1:2", "Double click did not send the selected verse");
        });
        Test("Bible preview keeps the new verse while output dissolves instead of jumping back to the old verse", () =>
        {
            Check(output.IsTransitioning, "Expected a real dissolve");
            Check(((FrameworkElement)window.FindName("PreviewHost")).Visibility == Visibility.Visible,
                "Prepared verse was replaced by the outgoing live frame");
            Check(Field<ProjectionSurface>(window, "_preview").DisplayedCue?.Reference == "Juan 1:2", "Preview lost the new verse");
            Until(() => !output.IsTransitioning, "Second verse did not finish transitioning");
            Until(() => ((FrameworkElement)window.FindName("PreviewLiveMonitor")).Visibility == Visibility.Visible,
                "Preview did not return to following live output");
            Check(output.DisplayedCue?.Reference == "Juan 1:2", "Output lost the selected verse");
        });
        if (!string.IsNullOrWhiteSpace(workspace.BibleStyle.BackgroundImagePath))
            Test("Bible preview and output reuse the decoded background instead of decoding it per verse", () =>
                Check(ReferenceEquals(Field<Image>(Field<ProjectionSurface>(window, "_preview"), "_image").Source,
                    Field<Image>(output, "_image").Source), "Same Bible background was decoded again"));
        Test("Bible background cache reloads a replaced file and does not show a deleted background", () =>
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".png");
            var preview = Field<ProjectionSurface>(window, "_preview");
            void WriteBackground(int width)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, 1, 96, 96, PixelFormats.Bgra32,
                    null, Enumerable.Repeat((byte)255, width * 4).ToArray(), width * 4)));
                using var stream = System.IO.File.Create(path);
                encoder.Save(stream);
            }
            try
            {
                preview.BibleStyle = workspace.BibleStyle with { BackgroundImagePath = path };
                WriteBackground(2);
                preview.ShowCue(session.Prepared);
                var first = Field<Image>(preview, "_image").Source;
                WriteBackground(3);
                System.IO.File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
                preview.ShowCue(session.Prepared);
                Check(!ReferenceEquals(first, Field<Image>(preview, "_image").Source)
                    && ((BitmapSource)Field<Image>(preview, "_image").Source).PixelWidth == 3, "Replaced background stayed cached");
                System.IO.File.Delete(path);
                preview.ShowCue(session.Prepared);
                Check(Field<Image>(preview, "_image").Source is null, "Deleted background stayed visible");
            }
            finally { System.IO.File.Delete(path); }
        });
        Test("Typing hebreo 5 1 prepares and sends canonical Hebreos 5:1", () =>
        {
            var live = session.Live;
            ((TextBox)window.FindName("BibleQuery")).Text = "hebreo 5 1";
            Call(window, "SearchBible");
            Check(session.Prepared?.Reference == "Hebreos 5:1", "Incomplete book name was not found");
            Check(ReferenceEquals(live, session.Live), "Search projected before an explicit send");
            Check((string)((ComboBox)window.FindName("BibleBookPicker")).SelectedItem == "Hebreos", "Book picker did not synchronize");
            Call(window, "ProjectBibleResult", results.SelectedItem);
            Until(() => !output.IsTransitioning, "Hebreos did not finish transitioning");
            Check(output.DisplayedCue?.Reference == "Hebreos 5:1", "Search result did not reach output");
        });
        Call(window, "CloseOutput");
    }
}
