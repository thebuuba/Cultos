using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;
using Culto.Desktop.Dialogs;
using Culto.Remote;

internal static partial class Program
{
    private static void RunVisualThemeTests(MainWindow window)
    {
        ((Slider)window.FindName("VolumeSlider")).Value = 0;
        Call(window, "DisposeYouTube");
        var workspace = Field<WorkspaceData>(window, "_workspace");
        workspace.Library.AddRange(new[]
        {
            new Cue { Kind = CueKind.Video, HymnNumber = 1, Title = "001 Cantad alegres al Señor", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") },
            new Cue { Kind = CueKind.Video, HymnNumber = 3, Title = "003 Unidos en espíritu", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") },
            new Cue { Kind = CueKind.Video, HymnNumber = 14, Title = "014 Engrandecido sea Dios", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") },
            new Cue { Kind = CueKind.Audio, Title = "Música de bienvenida", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }
        });
        Call(window, "RefreshLibrary");
        JsonFileStore.Save(Path.Combine(Field<WorkspaceStore>(window, "_store").DirectoryPath, "biblia.json"), new BibleDocument
        {
            Name = "Biblia de prueba", Attribution = "Texto sintético para validar el diseño",
            Verses = Enumerable.Range(14, 8).Select(number => new BibleVerse("Juan", 3, number,
                "Texto de prueba para comprobar la lectura, la selección y el espacio entre versículos.")).ToList()
        });
        Call(window, "LoadSavedBible");
        Call(window, "PrepareCue", (Cue)((ListBox)window.FindName("PlanList")).Items[0], null);
        Call(window, "ProjectPrepared");
        PumpFor(450);
        Call(window, "PrepareCue", new Cue { Kind = CueKind.Bible, Title = "Lectura del culto", Reference = "Lectura bíblica", Version = "Vista de prueba", Body = "Preparamos juntos el orden del culto." }, null);
        var folder = Environment.GetEnvironmentVariable("CULTO_DESIGN_CAPTURE_DIR") ?? Path.Combine(Environment.CurrentDirectory, ".tools", "visual-theme");
        Directory.CreateDirectory(folder);
        foreach (var size in new[] { new Size(1900, 1000), new Size(1480, 900), new Size(1280, 680), new Size(1200, 680) })
        {
            window.Width = size.Width; window.Height = size.Height;
            window.UpdateLayout(); PumpFor(180);
            if (size.Height == 680)
                Test($"Compact {size.Width} window keeps order and media actions visible with a usable output picture", () =>
                {
                    Call(window, "ActivateWorkspacePanel", "media"); window.UpdateLayout();
                    var client = (FrameworkElement)window.Content;
                    var dock = (FrameworkElement)window.FindName("WorkspaceDock");
                    foreach (var name in new[] { "AddOrderButton", "AddMediaToPlanButton" })
                        CheckWithin((FrameworkElement)window.FindName(name), client, BoundsWithin(dock, client), name);
                    Check(((FrameworkElement)window.FindName("OutputViewport")).ActualHeight >= 80, "Compact output picture is smaller than 80 DIP");
                });
            var path = Path.Combine(folder, $"console-{(int)size.Width}x{(int)size.Height}.png");
            CaptureDesignWindow(window, path);
        }
        window.Width = 1480; window.Height = 900; window.UpdateLayout();
        RunThemeTests(window);
        Test("Source strip is above its content and monitor pair has one shared card", () =>
        {
            Check(window.FindName("ScreenNavigationPanel") is null, "Removed Previous/Next row remains in the output card");
            var dock = (AvalonDock.DockingManager)window.FindName("WorkspaceDock");
            var source = (FrameworkElement)window.FindName("SourcesPanel");
            Call(window, "ActivateWorkspacePanel", "sources"); window.UpdateLayout();
            var pane = Descendants(dock).OfType<AvalonDock.Controls.LayoutAnchorablePaneControl>().Single(p => p.IsAncestorOf(source));
            var strip = Descendants(pane).OfType<AvalonDock.Controls.AnchorablePaneTabPanel>().Single();
            Check(BoundsWithin(strip, dock).Bottom <= BoundsWithin(source, dock).Top && pane.Clip is null, "Source tabs are below the body or have curved top clipping");
            var preview = (FrameworkElement)window.FindName("PreviewPanel"); var output = (FrameworkElement)window.FindName("LivePanel");
            var shared = Descendants(dock).OfType<AvalonDock.Controls.LayoutPanelControl>().Last(p => p.IsAncestorOf(preview) && p.IsAncestorOf(output));
            Check(shared.Background is SolidColorBrush && shared.Effect is not null && shared.Clip is not null, "Monitors have no shared card");
            foreach (var child in Descendants(shared).OfType<AvalonDock.Controls.LayoutAnchorablePaneControl>())
                Check(child.Margin == new Thickness(0) && child.Effect is null && child.Clip is null, "Monitor kept a separate rounded card");
        });
        Test("Bible utility buttons stay circular and aligned within their panel", () =>
        {
            Call(window, "ActivateWorkspacePanel", "bible"); window.UpdateLayout();
            var panel = (FrameworkElement)window.FindName("BiblePanel");
            foreach (var name in new[] { "Editar estilo de la Biblia", "Limpiar búsqueda de la Biblia", "Capítulo anterior", "Capítulo siguiente" })
            {
                var button = Descendants(panel).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == name);
                Check(Math.Abs(button.ActualWidth - 32) < .25 && Math.Abs(button.ActualHeight - 32) < .25, name + " is stretched");
                CheckWithin(button, panel, new Rect(panel.RenderSize), name);
            }
            var chapter = (FrameworkElement)window.FindName("BibleChapterPicker");
            var next = (FrameworkElement)window.FindName("NextBibleChapterButton");
            Check(Math.Abs(BoundsWithin(chapter, panel).Right - BoundsWithin(next, panel).Right) < 1, "Chapter selector and navigation have different right edges");
            CaptureDesignWindow(window, Path.Combine(folder, "bible-card.png"));
        });
        Test("YouTube source cards retain 16 by 9 thumbnails with long titles", () =>
        {
            Call(window, "ActivateWorkspacePanel", "youtube");
            var list = (ListBox)window.FindName("YouTubeList");
            list.ItemsSource = new[] { new YouTubeSource("test", new Cue { Kind = CueKind.WindowCapture, Title = "En Otro Tiempo Mas - Primera Fe (en canal Nuevo Tiempo Chile) - YouTube - Google Chrome" }, null, true) };
            ((TextBlock)window.FindName("YouTubeEmpty")).Visibility = Visibility.Collapsed;
            window.UpdateLayout(); PumpFor(80);
            var thumbnail = Descendants(list).OfType<CueThumbnail>().Single();
            Check(thumbnail.ActualWidth <= 260 && Math.Abs(thumbnail.ActualWidth / thumbnail.ActualHeight - 16d / 9) < .04, "YouTube thumbnail stretches with its panel width");
            CaptureDesignWindow(window, Path.Combine(folder, "youtube-card.png"));
        });

        Test("Primary controls and live order text retain readable contrast", () =>
        {
            var project = (Button)window.FindName("ProjectButton");
            Check(ThemeContrast(project.Foreground, project.Background) >= 4.5, "Primary action text lacks contrast");
            foreach (var label in Descendants(project).OfType<TextBlock>())
                Check(ThemeContrast(label.Foreground, project.Background, label.Opacity) >= 4.5, "Primary action label lacks contrast: " + label.Text);
            var list = (ListBox)window.FindName("PlanList");
            list.ScrollIntoView(list.Items[0]); list.UpdateLayout();
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            row.ApplyTemplate();
            var surface = (Border)row.Template.FindName("Row", row);
            Check(surface.Background is SolidColorBrush brush && brush.Color.A == 255, "The live row has no visible state surface");
            foreach (var label in Descendants(row).OfType<TextBlock>().Where(label => label.IsVisible && label.Text.Length > 0))
                Check(ThemeContrast(label.Foreground, surface.Background) >= 4.5, "Live order text lacks contrast: " + label.Text);
            var muted = (Brush)window.FindResource("MutedBrush");
            var raised = (Brush)window.FindResource("RaisedBrush");
            Check(ThemeContrast(muted, raised) >= 4.5, "Metadata is unreadable on raised controls");
        });
        Test("Remote dialog fits its QR and full phone deck at compact desktop height", () =>
        {
            var state = (RemoteSnapshot)Call(window, "BuildRemoteState")!;
            var host = new RemoteHost();
            Wait(host.StartAsync(0, () => state, _ => Task.FromResult(new CommandResult(true, "Prueba")), true));
            var dialog = new RemoteDialog(host, workspace.Deck, workspace.Library) { Owner = window, SnapshotRequested = () => state };
            try
            {
                var addresses = Field<ComboBox>(dialog, "_addresses");
                addresses.ItemsSource = new[] { "127.0.0.1" }; addresses.SelectedIndex = 0;
                Call(dialog, "RefreshConnection");
                dialog.Height = 680; dialog.Show(); dialog.UpdateLayout(); PumpFor(100);
                var qr = Field<Image>(dialog, "_qr");
                Check(qr.Source is not null, "Real pairing host produced no QR");
                var phone = Descendants(dialog).OfType<Border>().Single(border => System.Windows.Automation.AutomationProperties.GetName(border).StartsWith("Vista previa del mando con"));
                var deck = Field<System.Windows.Controls.Primitives.UniformGrid>(dialog, "_phoneDeck");
                foreach (FrameworkElement tile in deck.Children)
                    CheckWithin(tile, phone, new Rect(phone.RenderSize), "Remote phone tile");
                Check(Field<TextBlock>(dialog, "_phoneBody").Text == state.LiveBody, "Phone preview does not display actual projected text");
                CaptureDesignWindow(dialog, Path.Combine(folder, "control-remoto.png"));
                Call(dialog, "ShowDeck", true); dialog.UpdateLayout(); PumpFor(60);
                Check(Field<ListBox>(dialog, "_list").IsVisible, "Deck editor is unreachable from connection view");
                List<DeckButton>? saved = null; dialog.DeckChanged = buttons => saved = buttons;
                Field<TextBox>(dialog, "_label").Text = "Acceso de prueba";
                Call(dialog, "SaveButton");
                Check(saved?.Any(button => button.Label == "Acceso de prueba") == true, "The redesigned editor does not save deck changes");
                CaptureDesignWindow(dialog, Path.Combine(folder, "control-remoto-editor.png"));
            }
            finally { dialog.Close(); Wait(host.DisposeAsync().AsTask()); }
        });
    }

    private static void CaptureDesignWindow(Window window, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var canvas = new DrawingVisual();
        using (var drawing = canvas.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(window.RenderSize));
        bitmap.Render(canvas); bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
        Console.WriteLine("CAPTURE " + path);
    }
    private static double ThemeContrast(Brush foreground, Brush background, double opacity = 1)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte channel)
            {
                var value = channel / 255d;
                return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
        var ink = ((SolidColorBrush)foreground).Color;
        var paper = ((SolidColorBrush)background).Color;
        var alpha = opacity * foreground.Opacity * ink.A / 255d;
        var effective = Color.FromRgb((byte)Math.Round(ink.R * alpha + paper.R * (1 - alpha)),
            (byte)Math.Round(ink.G * alpha + paper.G * (1 - alpha)), (byte)Math.Round(ink.B * alpha + paper.B * (1 - alpha)));
        var first = Luminance(effective); var second = Luminance(paper);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

}
