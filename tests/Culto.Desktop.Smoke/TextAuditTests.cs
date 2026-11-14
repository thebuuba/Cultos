using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;
using AvalonDock;
using AvalonDock.Layout;

internal static partial class Program
{
    private static void RunTextAuditTests(MainWindow owner)
    {
        Test("All desktop and remote UI source text decodes as UTF8", () =>
        {
            var utf8 = new System.Text.UTF8Encoding(false, true);
            foreach (var directory in new[] { "src/Culto.Desktop", "src/Culto.Remote" })
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                    .Where(file => new[] { ".cs", ".xaml", ".html" }.Contains(Path.GetExtension(file))
                        && !file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")))
                    utf8.GetString(File.ReadAllBytes(file));
        });
        var folder = Path.Combine(Environment.CurrentDirectory, ".tools", "text-audit");
        Directory.CreateDirectory(folder);
        var findings = new List<string>();
        var coverage = new List<string>();
        var font = (FontFamily)owner.FindResource("ConsoleFont");
        foreach (var family in new[] { font, new FontFamily("Segoe UI") })
            if (new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var glyph))
                coverage.Add($"Resolved font {family.Source}: {glyph.FontUri}");
        bool IsProjection(DependencyObject node)
        {
            for (DependencyObject? parent = node; parent is not null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is ProjectionSurface) return true;
            return false;
        }
        void Inspect(FrameworkElement surface, string name)
        {
            surface.UpdateLayout(); PumpFor(50);
            var labels = Descendants(surface).OfType<TextBlock>().Where(t => t.IsVisible && t.Text.Any(char.IsLetterOrDigit) && !IsProjection(t)).ToArray();
            coverage.Add($"{name}: {labels.Length} rendered labels");
            foreach (var label in labels)
            {
                var sample = label.Text.Replace('\n', ' ');
                if (sample.Length > 65) { sample = sample[..65]; if (char.IsHighSurrogate(sample[^1])) sample = sample[..^1]; }
                if (label.FontSize < 12) findings.Add($"{name}: small text ({label.FontSize}): {sample}");
                var root = PresentationSource.FromVisual(label)?.RootVisual ?? surface;
                var pixelScale = label.TransformToAncestor(root).TransformBounds(new Rect(0, 0, 1, 1));
                if (label.FontSize * Math.Min(pixelScale.Width, pixelScale.Height) < 11.9)
                    findings.Add($"{name}: a transform shrinks text below readable size: {sample}");
                if (!label.FontFamily.Equals(font) && !label.FontFamily.Source.Contains("Consolas") && !label.FontFamily.Source.Contains("MDL2"))
                    findings.Add($"{name}: unexpected font ({label.FontFamily.Source}): {sample}");
                for (DependencyObject? node = label; node is Visual; node = VisualTreeHelper.GetParent(node))
                {
                    if (node is UIElement { Effect: DropShadowEffect })
                    { findings.Add($"{name}: text is rasterized by a container shadow: {sample}"); break; }
                }
            }
            if (surface is Window window) CaptureDesignWindow(window, Path.Combine(folder, name + ".png"));
        }
        void InspectMenu(ContextMenu menu, FrameworkElement target, string name)
        {
            menu.PlacementTarget = target; menu.IsOpen = true; PumpFor(30);
            try
            {
                Inspect(menu, name);
                foreach (var item in menu.Items.OfType<MenuItem>().Where(item => item.HasItems))
                {
                    item.IsSubmenuOpen = true; PumpFor(30);
                    foreach (var child in item.Items.OfType<MenuItem>().Where(child => child.IsVisible)) Inspect(child, name + "-submenu");
                    item.IsSubmenuOpen = false;
                }
            }
            finally { menu.IsOpen = false; PumpFor(30); }
        }
        foreach (var size in new[] { new Size(1480, 900), new Size(1200, 680) })
        {
            owner.Width = size.Width; owner.Height = size.Height;
            foreach (var id in new[] { "sources", "bible", "youtube", "media", "text", "playlists", "powerpoint" })
            {
                Call(owner, "ActivateWorkspacePanel", id);
                Inspect(owner, $"console-{id}-{size.Width}");
            }
        }
        owner.Width = 1480; owner.Height = 900; owner.UpdateLayout();
        foreach (var button in Descendants(owner).OfType<Button>().Where(b => b.IsVisible && b.ContextMenu is not null).ToArray())
            InspectMenu(button.ContextMenu, button, "menu-" + button.Name);
        var dock = (DockingManager)owner.FindName("WorkspaceDock");
        var scenes = dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(p => p.ContentId == "scenes");
        scenes.Float(); owner.UpdateLayout(); PumpFor(100);
        foreach (var floating in dock.FloatingWindows) Inspect(floating, "floating-panel");
        scenes.Dock(); owner.UpdateLayout(); PumpFor(100);
        var cue = new Cue { Title = "Canción y proyección: información con acentos", Kind = CueKind.Text, Body = "Texto de prueba con acentos: áéíóú, ñ y números 0123456789." };
        var dragType = typeof(MainWindow).Assembly.GetType("Culto.Desktop.ContentDragPreview")!;
        using (var drag = (IDisposable)Activator.CreateInstance(dragType, [(FrameworkElement)owner.FindName("PlanList"), cue, false])!)
            Inspect((FrameworkElement)Field<Popup>(drag, "_popup").Child, "drag-preview");
        var workspace = Field<WorkspaceData>(owner, "_workspace");
        var dialogs = new (string Name, Func<Window> Create)[]
        {
            ("edit-content", () => new CueEditDialog(cue)),
            ("display", () => new DisplayDialog(DisplayService.GetDisplays(), null)),
            ("capture-display", () => new DisplayDialog(DisplayService.GetDisplays(), null, true)),
            ("unsaved-changes", () => new UnsavedChangesDialog()),
            ("transitions", () => new TransitionDialog(new())),
            ("browser-source", () => new BrowserSourceDialog()),
            ("browser-sources", () => new BrowserSourcesDialog([])),
            ("saved-sets", () => new SavedSetsDialog([new SavedSet { Name = cue.Title, Items = [cue] }])),
            ("background-gallery", () => new BackgroundGalleryDialog([], null, true)),
            ("logo-settings", () => new LogoSettingsDialog([], null, new())),
            ("bible-style", () => new BibleStyleDialog(new(), cue)),
            ("window-capture", () => new WindowCaptureDialog()),
            ("output-player", () => new OutputPlayerWindow()),
            ("remote", () => new RemoteDialog(null, workspace.Deck, workspace.Library)),
            ("prompt", () => (Window)Activator.CreateInstance(typeof(PromptDialog), BindingFlags.Instance | BindingFlags.NonPublic,
                null, ["Nombre del culto", "Escribe un nombre con acentos y nÃºmeros.", cue.Title], null)!)
        };
        foreach (var (name, create) in dialogs)
        {
            var dialog = create(); dialog.Owner = owner;
            try
            {
                dialog.Show(); Inspect(dialog, name);
                if (dialog.SizeToContent == SizeToContent.Manual)
                {
                    dialog.Width = Math.Max(dialog.MinWidth, Math.Min(dialog.Width, 640));
                    dialog.Height = Math.Max(dialog.MinHeight, Math.Min(dialog.Height, 680));
                    Inspect(dialog, name + "-compact");
                }
                if (dialog is RemoteDialog remote)
                {
                    Call(remote, "ShowDeck", true);
                    Inspect(remote, "remote-editor");
                }
                foreach (var combo in Descendants(dialog).OfType<ComboBox>().Where(c => c.IsVisible && c.Items.Count > 0).ToArray())
                {
                    combo.IsDropDownOpen = true; PumpFor(30);
                    combo.IsDropDownOpen = false;
                }
            }
            finally { dialog.Close(); PumpFor(50); }
        }
        File.WriteAllLines(Path.Combine(folder, "coverage.txt"), coverage);
        File.WriteAllLines(Path.Combine(folder, "findings.txt"), findings.Distinct());
        Test("Every desktop screen and dialog keeps readable text without container rasterization", () =>
            Check(findings.Count == 0, $"{findings.Count} typography issues; see .tools/text-audit/findings.txt"));
        RunBibleStyleIntegrationTests(owner);
        RunBibleEditorTests(owner);
    }
}

