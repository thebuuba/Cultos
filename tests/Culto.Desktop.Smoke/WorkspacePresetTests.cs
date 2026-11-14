using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunWorkspacePresetTests(MainWindow window, string directory)
    {
        string[] expectedIds = ["bible", "media", "order", "playlists", "powerpoint", "preview", "scenes", "sources", "text", "youtube"];
        string[] sourceIds = ["sources", "bible", "text", "youtube", "playlists", "powerpoint"];
        var manager = (DockingManager)window.FindName("WorkspaceDock");
        var session = Field<PresentationSession>(window, "_session");
        var originalLayout = (string)Call(window, "SerializeDockLayout")!;
        var originalContents = manager.Layout.Descendents().OfType<LayoutAnchorable>().ToDictionary(p => p.ContentId, p => p.Content);
        var originalWidth = window.Width; var originalHeight = window.Height;
        var originalLeft = window.Left; var originalTop = window.Top; var originalState = window.WindowState;
        var originalLive = session.Live; var originalPrepared = session.Prepared;
        var originalBlackout = session.IsBlackout; var originalHidden = session.IsTextHidden;
        var layoutPath = Path.Combine(directory, "paneles-v3.xml");
        var legacyPath = Path.Combine(directory, "paneles.xml");
        var originalSavedLayout = File.Exists(layoutPath) ? File.ReadAllBytes(layoutPath) : null;
        var originalLegacyLayout = File.Exists(legacyPath) ? File.ReadAllBytes(legacyPath) : null;

        LayoutAnchorable Panel(string id) => manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(p => p.ContentId == id);
        FrameworkElement Content(string id) => (FrameworkElement)Panel(id).Content;
        void Settle() { window.UpdateLayout(); PumpFor(100); window.UpdateLayout(); }
        void Preset(string id)
        {
            Call(window, "WorkspacePresetClick", new MenuItem { Tag = id }, new RoutedEventArgs());
            Settle();
        }

        try
        {
            window.WindowState = WindowState.Normal;
            window.Width = 1900; window.Height = 1000;
            window.Left = SystemParameters.WorkArea.Left; window.Top = SystemParameters.WorkArea.Top;
            Settle();
            // Non-null, independent fixtures make identity checks cover both
            // prepared and live content without opening an output window.
            session.Prepare(new Cue { Title = "Salida de prueba", Body = "El contenido en salida se conserva." });
            session.GoLive();
            session.Prepare(new Cue { Title = "Preparado de prueba", Body = "El contenido preparado se conserva." });
            var live = session.Live; var prepared = session.Prepared;
            var liveBody = live!.Body; var preparedBody = prepared!.Body;

            void CheckWorkspace(string context)
            {
                var panels = manager.Layout.Descendents().OfType<LayoutAnchorable>().ToArray();
                Check(panels.Length == expectedIds.Length && panels.Select(p => p.ContentId).Distinct().Count() == expectedIds.Length
                    && panels.Select(p => p.ContentId).Order().SequenceEqual(expectedIds), context + ": missing or duplicate panel IDs");
                foreach (var panel in panels)
                    Check(ReferenceEquals(panel.Content, originalContents[panel.ContentId]), context + ": recreated " + panel.ContentId);
                Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), context + ": replaced live/prepared cues");
                Check(session.Live!.Body == liveBody && session.Prepared!.Body == preparedBody, context + ": mutated presentation content");
            }

            void CheckSourceTabs(string context, bool includeMedia = false)
            {
                var expected = includeMedia ? sourceIds.Append("media").ToArray() : sourceIds;
                var pane = (LayoutAnchorablePane)Panel("sources").Parent;
                Check(expected.All(id => ReferenceEquals(Panel(id).Parent, pane)), context + ": sources do not share one pane");
                Check(pane.Children.Select(p => p.ContentId).Order().SequenceEqual(expected.Order()), context + ": unexpected source tabs");
                var nativePane = Descendants(manager).OfType<LayoutAnchorablePaneControl>().Single(control => ReferenceEquals(control.Model, pane));
                var tabs = Descendants(nativePane).OfType<LayoutAnchorableTabItem>().Where(tab => tab.IsVisible).ToArray();
                Check(tabs.Select(tab => tab.Model.ContentId).Order().SequenceEqual(expected.Order()), context + ": missing native AvalonDock tabs");
            }

            void CheckPrincipalCards()
            {
                string[] visibleIds = ["sources", "scenes", "preview", "order"];
                var cards = visibleIds.Select(id => (Id: id, Element: Content(id))).ToArray();
                foreach (var card in cards) CheckWithin(card.Element, manager, new Rect(manager.RenderSize), "Principal " + card.Id);
                foreach (var name in new[] { "PreviewViewport", "OutputViewport" })
                    CheckWithin((FrameworkElement)window.FindName(name), Content("preview"), new Rect(Content("preview").RenderSize), name);
                for (var first = 0; first < cards.Length; first++)
                    for (var second = first + 1; second < cards.Length; second++)
                    {
                        var overlap = Rect.Intersect(BoundsWithin(cards[first].Element, manager), BoundsWithin(cards[second].Element, manager));
                        Check(overlap.IsEmpty || overlap.Width <= 1 || overlap.Height <= 1,
                            "Principal cards overlap: " + cards[first].Id + " and " + cards[second].Id);
                    }
                CheckSourceTabs("Principal", includeMedia: true);
                foreach (var id in sourceIds.Append("media"))
                {
                    Call(window, "ActivateWorkspacePanel", id); Settle();
                    Check(Panel(id).IsSelected, "Source tab is not selected: " + id);
                    foreach (var other in new[] { "preview", "order" })
                        Check(Content(other).IsVisible, "Changing tabs hid " + other);
                }
                Call(window, "ActivateWorkspacePanel", "sources"); Settle();
            }

            Test("Workspace presets retain all ten IDs, controls and presentation snapshots across repeated switches", () =>
            {
                foreach (var preset in new[] { "principal", "compacta", "proyeccion", "principal", "proyeccion", "compacta" })
                {
                    Preset(preset);
                    CheckWorkspace(preset);
                }
            });

            Test("Principal groups seven sources while order and both monitors remain visible", () =>
            {
                Preset("principal");
                CheckPrincipalCards();
                CheckWorkspace("Principal geometry");
            });

            Test("Compacta keeps seven source tabs to the left of both monitors", () =>
            {
                Preset("compacta");
                CheckSourceTabs("Compacta", includeMedia: true);
                var sources = BoundsWithin(Content("sources"), manager);
                Check(sources.Right <= BoundsWithin(Content("preview"), manager).Left + 1, "Compacta sources are not on the left");
                foreach (var id in sourceIds.Append("media"))
                {
                    Call(window, "ActivateWorkspacePanel", id); Settle();
                    Check(Panel(id).IsSelected, "Native source tab did not select " + id);
                    CheckWithin(Content(id), manager, new Rect(manager.RenderSize), "Compacta " + id);
                }
                CheckWorkspace("Compacta tab activation");
            });

            Test("Proyeccion groups sources beside larger monitor pictures than Principal", () =>
            {
                double PictureArea(string name)
                {
                    var viewport = (FrameworkElement)window.FindName(name);
                    return viewport.ActualWidth * viewport.ActualHeight;
                }
                Preset("principal");
                var previewArea = PictureArea("PreviewViewport"); var outputArea = PictureArea("OutputViewport");
                Check(previewArea > 0 && outputArea > 0, "Principal monitor pictures have no area");
                Preset("proyeccion");
                CheckSourceTabs("Proyeccion");
                Check(PictureArea("PreviewViewport") > previewArea * 1.25 && PictureArea("OutputViewport") > outputArea * 1.25,
                    "Proyeccion did not enlarge both monitor pictures");
                var sourceBounds = BoundsWithin(Content("sources"), manager);
                Check(sourceBounds.Right <= BoundsWithin(Content("preview"), manager).Left + 1, "Proyeccion sources are not beside the monitors");
                Check(!ReferenceEquals(Panel("media").Parent, Panel("order").Parent), "Proyeccion grouped order with media");
                CheckWorkspace("Proyeccion geometry");
            });

            Test("Customized grouping and floating survive paneles-v3 round-trip and window resizing", () =>
            {
                File.WriteAllText(legacyPath, "Legacy layout must remain untouched.");
                Preset("compacta");
                var order = Panel("order"); var target = (LayoutAnchorablePane)Panel("media").Parent;
                ((LayoutAnchorablePane)order.Parent).Children.Remove(order); target.Children.Add(order); order.IsSelected = true;
                target.DockWidth = new GridLength(1.35, GridUnitType.Star);
                Panel("youtube").Float(); Settle();
                var groupWidth = target.DockWidth;
                Check((bool)Call(window, "SaveDockLayout")!, "Could not save customized workspace");
                Check(File.Exists(layoutPath), "Workspace was not saved to paneles-v3.xml");
                Check(File.ReadAllText(legacyPath) == "Legacy layout must remain untouched.", "Saving replaced the legacy layout file");
                var saved = File.ReadAllText(layoutPath);
                Check(XDocument.Parse(saved).Descendants().Attributes("ContentId").Count() == expectedIds.Length, "Saved workspace lost panels");
                Call(window, "RestoreDockLayout", originalLayout); Settle();
                Call(window, "RestoreDockLayout", saved); Settle();
                Check(ReferenceEquals(Panel("order").Parent, Panel("media").Parent), "Restoring lost customized grouping");
                Check(((LayoutAnchorablePane)Panel("media").Parent).DockWidth == groupWidth, "Restoring lost customized group width");
                Check(Panel("youtube").IsFloating, "Restoring lost floating YouTube");
                Check(PresentationSource.FromVisual(Content("youtube")) != PresentationSource.FromVisual(window), "Restored floating panel has no separate native surface");
                var customizedRoot = manager.Layout;
                window.Width = 1200; Settle(); window.Width = 1900; Settle();
                Check(ReferenceEquals(customizedRoot, manager.Layout), "Window resizing replaced the customized layout");
                CheckWorkspace("Customized layout round-trip");
            });

            Test("Complete six-panel legacy layout migrates to all current panels without resetting custom geometry", () =>
            {
                Preset("compacta");
                var legacy = XDocument.Parse((string)Call(window, "SerializeDockLayout")!);
                legacy.Descendants("LayoutAnchorable").Single(e => (string?)e.Attribute("ContentId") == "scenes").Parent!.Remove();
                foreach (var id in new[] { "bible", "text", "youtube", "playlists", "powerpoint" })
                    legacy.Descendants("LayoutAnchorable").Single(element => (string?)element.Attribute("ContentId") == id).Remove();
                var sources = legacy.Descendants("LayoutAnchorable").Single(element => (string?)element.Attribute("ContentId") == "sources");
                sources.SetAttributeValue("IsSelected", true);
                sources.Parent!.SetAttributeValue("DockWidth", "1.37*");
                sources.Parent.SetAttributeValue("DockHeight", "0.91*");
                var order = legacy.Descendants("LayoutAnchorable").Single(element => (string?)element.Attribute("ContentId") == "order");
                order.Parent!.SetAttributeValue("DockWidth", "0.83*");
                order.Parent.Add(new XElement("LayoutAnchorable", new XAttribute("Title", "Retired"), new XAttribute("ContentId", "sequence")));
                order.Parent.Add(new XElement("LayoutAnchorable", new XAttribute("Title", "Old output"), new XAttribute("ContentId", "output")));
                Check(legacy.Descendants().Attributes("ContentId").Count() == 6, "Legacy fixture is not a complete six-panel layout");
                Call(window, "RestoreDockLayout", legacy.ToString()); Settle();
                CheckSourceTabs("Migrated legacy layout", includeMedia: true);
                var migratedSources = (LayoutAnchorablePane)Panel("sources").Parent;
                Check(migratedSources.DockWidth == new GridLength(1.37, GridUnitType.Star)
                    && migratedSources.DockHeight == new GridLength(.91, GridUnitType.Star), "Migration reset source group geometry");
                Check(((LayoutAnchorablePane)Panel("order").Parent).DockWidth == new GridLength(.83, GridUnitType.Star), "Migration reset unrelated order geometry");
                CheckWorkspace("Six-panel migration");
                Call(window, "RestoreDockLayout", (string)Call(window, "SerializeDockLayout")!); Settle();
                CheckSourceTabs("Second legacy restore", includeMedia: true);
                CheckWorkspace("Idempotent migration");
            });

            Test("Corrupt XML and duplicate IDs fail before the live layout is replaced", () =>
            {
                Preset("compacta");
                var duplicate = XDocument.Parse((string)Call(window, "SerializeDockLayout")!);
                duplicate.Descendants("LayoutAnchorable").Single(element => (string?)element.Attribute("ContentId") == "scenes")
                    .SetAttributeValue("ContentId", "preview");
                foreach (var invalid in new[] { "<LayoutRoot>", "<LayoutRoot />", duplicate.ToString() })
                {
                    var before = manager.Layout;
                    var rejected = false;
                    try { Call(window, "RestoreDockLayout", invalid); }
                    catch (TargetInvocationException error) when (error.InnerException is XmlException or InvalidOperationException) { rejected = true; }
                    Check(rejected && ReferenceEquals(before, manager.Layout), "Invalid layout changed the live workspace");
                    CheckWorkspace("Rejected invalid layout");
                }
            });

            Test("Reset returns all ten panels to Principal and closes floating hosts", () =>
            {
                Preset("compacta"); Panel("text").Float(); Settle();
                Check(Panel("text").IsFloating, "Reset fixture did not float a panel");
                Call(window, "ResetDockLayoutClick", new MenuItem(), new RoutedEventArgs()); Settle();
                Check(manager.FloatingWindows.Count() == 0, "Reset left a floating host open");
                CheckPrincipalCards();
                CheckWorkspace("Reset Principal");
            });
        }
        finally
        {
            window.WindowState = WindowState.Normal;
            window.Width = originalWidth; window.Height = originalHeight; window.Left = originalLeft; window.Top = originalTop;
            Settle();
            Call(window, "RestoreDockLayout", originalLayout); Settle();
            window.WindowState = originalState;
            session.RestoreOutput(originalLive, originalBlackout, originalHidden);
            typeof(PresentationSession).GetProperty(nameof(PresentationSession.Live))!.SetValue(session, originalLive);
            typeof(PresentationSession).GetProperty(nameof(PresentationSession.Prepared))!.SetValue(session, originalPrepared);
            Call(window, "UpdateSessionLabels");
            if (originalSavedLayout is null) File.Delete(layoutPath); else File.WriteAllBytes(layoutPath, originalSavedLayout);
            if (originalLegacyLayout is null) File.Delete(legacyPath); else File.WriteAllBytes(legacyPath, originalLegacyLayout);
        }
    }
}
