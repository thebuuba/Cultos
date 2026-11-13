using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunPanelSizingTests(MainWindow window)
    {
        var manager = (DockingManager)window.FindName("WorkspaceDock");
        // Exercise rearrangement with separate source cards as well as tabs.
        Call(window, "PlaceWorkspacePanel", "bible", "sources", "right");
        Call(window, "PlaceWorkspacePanel", "youtube", "bible", "right");
        var initial = (string)Call(window, "SerializeDockLayout")!;
        var width = window.Width; var height = window.Height;
        var session = Field<PresentationSession>(window, "_session");
        session.Prepare(new Cue { Title = "Salida conservada", Body = "Salida de prueba" }); session.GoLive();
        session.Prepare(new Cue { Title = "Preparado conservado", Body = "Otra prueba" });
        var live = session.Live; var prepared = session.Prepared;
        LayoutAnchorable Panel(string id) => manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(panel => panel.ContentId == id);
        Dictionary<string, XElement> Contents() => XDocument.Parse(initial).Descendants("LayoutAnchorable")
            .ToDictionary(element => (string)element.Attribute("ContentId")!, element => new XElement(element));
        XElement Pane(params XElement[] contents) => new("LayoutAnchorablePane", new XAttribute("DockWidth", "*"), contents);
        void Restore(XElement root)
        {
            var doc = XDocument.Parse(initial); doc.Root!.Attribute("WorkspacePreset")?.Remove();
            doc.Root.Element("RootPanel")!.ReplaceWith(root);
            doc.Root.Element("FloatingWindows")?.RemoveNodes(); doc.Root.Element("Hidden")?.RemoveNodes();
            Call(window, "RestoreDockLayout", doc.ToString()); window.UpdateLayout(); PumpFor(100);
        }
        void AssertSession() => Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Layout editing replaced the presentation");
        GridLength Width(ILayoutPanelElement child) => child switch
        { LayoutPanel group => group.DockWidth, LayoutAnchorablePane pane => pane.DockWidth, _ => throw new InvalidOperationException() };
        try
        {
            Test("Bottom corners and the tab strip select a bottom dock destination", () =>
            {
                window.Width = 1600; window.Height = 900;
                Call(window, "RestoreDockLayout", initial); window.UpdateLayout(); PumpFor(80);
                var content = (FrameworkElement)Panel("sources").Content;
                var pane = Descendants(manager).OfType<LayoutAnchorablePaneControl>().Single(p => ReferenceEquals(p.Model, Panel("sources").Parent));
                foreach (var point in new[] { new Point(12, pane.ActualHeight - 3), new Point(pane.ActualWidth - 12, pane.ActualHeight - 3) })
                {
                    Call(window, "BeginPanelDrag", "youtube");
                    Call(window, "UpdatePanelDrag", pane.PointToScreen(point));
                    Check(Field<string?>(window, "_dropPanelSide") == "bottom", "Bottom edge selected a lateral zone or no zone");
                    Call(window, "FinishPanelDrag", false);
                }
            });
            Test("Repeated vertical rearrangement keeps a flat stack and stays in the workspace", () =>
            {
                Call(window, "RestoreDockLayout", initial);
                Call(window, "PlaceWorkspacePanel", "youtube", "bible", "bottom");
                for (var i = 0; i < 6; i++)
                {
                    Call(window, "PlaceWorkspacePanel", "youtube", "bible", i % 2 == 0 ? "top" : "bottom");
                    window.UpdateLayout();
                    var document = XDocument.Parse((string)Call(window, "SerializeDockLayout")!);
                    Check(!document.Descendants("LayoutPanel").Any(e => e.Elements().Count() == 1), "Rearranging accumulated single-child layout groups");
                    foreach (var id in new[] { "youtube", "bible" })
                        CheckWithin((FrameworkElement)Panel(id).Content, manager, new Rect(manager.RenderSize), id);
                }
                AssertSession();
            });
            Test("Header drag highlights and drops above and below a panel without floating", () =>
            {
                window.Width = 1600; window.Height = 900;
                foreach (var bottom in new[] { false, true })
                {
                    Call(window, "RestoreDockLayout", initial); window.UpdateLayout(); PumpFor(80);
                    var target = (FrameworkElement)Panel("bible").Content;
                    var point = target.PointToScreen(new Point(target.ActualWidth / 2, bottom ? target.ActualHeight - 5 : 5));
                    Call(window, "BeginPanelDrag", "sources");
                    Call(window, "UpdatePanelDrag", point);
                    Check(Field<object?>(window, "_panelDropHint") is not null, "Dragging shows no destination highlight");
                    Call(window, "FinishPanelDrag", true); window.UpdateLayout();
                    var a = BoundsWithin((FrameworkElement)Panel("sources").Content, manager);
                    var b = BoundsWithin((FrameworkElement)Panel("bible").Content, manager);
                    Check(bottom ? a.Top >= b.Bottom : a.Bottom <= b.Top, "Pointer drop did not split vertically");
                    Check(!Panel("sources").IsFloating && !manager.FloatingWindows.Any(), "Dragging created a floating window");
                    AssertSession();
                }
                var before = (string)Call(window, "SerializeDockLayout")!;
                Call(window, "BeginPanelDrag", "sources");
                var bible = (FrameworkElement)Panel("bible").Content;
                Call(window, "UpdatePanelDrag", bible.PointToScreen(new Point(5, 10)));
                Call(window, "FinishPanelDrag", false);
                Check(before == (string)Call(window, "SerializeDockLayout")! && Field<object?>(window, "_panelDropHint") is null,
                    "Cancel changed layout or left a highlight behind");
            });
            Test("A constrained star group resizes immediately without taking space from a third neighbour", () =>
            {
                window.Width = 1450; window.Height = 900; window.UpdateLayout();
                var items = Contents();
                var first = new XElement("LayoutPanel", new XAttribute("Orientation", "Horizontal"), new XAttribute("DockWidth", "*"),
                    Pane(items["sources"]), Pane(items["bible"]), Pane(items["youtube"]));
                Restore(new XElement("RootPanel", new XAttribute("Orientation", "Horizontal"), first, Pane(items["output"]),
                    Pane(items.Where(entry => entry.Key is not ("sources" or "bible" or "youtube" or "output")).Select(entry => entry.Value).ToArray())));
                var grid = Descendants(manager).OfType<LayoutPanelControl>().Single(control => ReferenceEquals(control.Model, manager.Layout.RootPanel));
                var splitter = grid.Children.OfType<LayoutGridResizerControl>().First();
                var a = grid.ColumnDefinitions[0].ActualWidth;
                var b = grid.ColumnDefinitions[2].ActualWidth;
                var c = grid.ColumnDefinitions[4].ActualWidth;
                var original = manager.Layout.RootPanel.Children.Select(Width).ToArray();
                splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                try
                {
                    splitter.RaiseEvent(new DragDeltaEventArgs(20, 0) { RoutedEvent = Thumb.DragDeltaEvent });
                    window.UpdateLayout();
                    Console.WriteLine($"RESIZE {a:0.0}/{b:0.0}/{c:0.0} -> {grid.ColumnDefinitions[0].ActualWidth:0.0}/{grid.ColumnDefinitions[2].ActualWidth:0.0}/{grid.ColumnDefinitions[4].ActualWidth:0.0}");
                    Check(Math.Abs(grid.ColumnDefinitions[0].ActualWidth - a - 20) < 2, "The constrained group does not follow the first 20 DIP drag");
                    Check(Math.Abs(grid.ColumnDefinitions[2].ActualWidth - b + 20) < 2, "The adjacent panel does not give up the requested space");
                    Check(Math.Abs(grid.ColumnDefinitions[4].ActualWidth - c) < 2, "Resizing moved an unrelated third panel");
                    AssertSession();
                }
                finally { splitter.RaiseEvent(new DragCompletedEventArgs(20, 0, true) { RoutedEvent = Thumb.DragCompletedEvent }); window.UpdateLayout(); }
                Check(manager.Layout.RootPanel.Children.Select(Width).SequenceEqual(original), "Cancel did not restore all original star weights");
            });
            Test("Himnario and Bible stack at useful short heights with every action reachable", () =>
            {
                JsonFileStore.Save(System.IO.Path.Combine(Field<WorkspaceStore>(window, "_store").DirectoryPath, "biblia.json"), new BibleDocument
                {
                    Name = "Biblia de prueba", Attribution = "Texto sintético",
                    Verses = Enumerable.Range(1, 31).Select(number => new BibleVerse("Génesis", 1, number, "Texto de prueba para verificar el panel apilado y su lista.")).ToList()
                });
                Call(window, "LoadSavedBible");
                window.Width = 1400; window.Height = 740; window.UpdateLayout();
                var items = Contents();
                Restore(new XElement("RootPanel", new XAttribute("Orientation", "Horizontal"),
                    new XElement("LayoutPanel", new XAttribute("Orientation", "Vertical"), new XAttribute("DockWidth", "*"), Pane(items["sources"]), Pane(items["bible"])),
                    Pane(items.Where(entry => entry.Key is not ("sources" or "bible")).Select(entry => entry.Value).ToArray())));
                foreach (var name in new[] { "SourcesPanel", "BiblePanel" })
                {
                    var panel = (FrameworkElement)window.FindName(name);
                    CheckWithin(panel, manager, new Rect(manager.RenderSize), name);
                    Check(panel.ActualHeight < 320, name + " retains an oversized minimum height");
                    foreach (var control in Descendants(panel).OfType<FrameworkElement>().Where(element => element.IsVisible && element is Button or ComboBox or TextBox))
                    {
                        control.BringIntoView(); window.UpdateLayout();
                        CheckWithin(control, panel, new Rect(panel.RenderSize), "Reachable " + control.Name);
                    }
                }
                var captures = System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "panel-sizing");
                System.IO.Directory.CreateDirectory(captures);
                foreach (var name in new[] { "SourcesPanel", "BiblePanel" })
                    ((ScrollViewer)((Border)window.FindName(name)).Child).ScrollToTop();
                window.UpdateLayout(); CaptureDesignWindow(window, System.IO.Path.Combine(captures, "stacked-panels.png"));
                AssertSession();
            });
            Test("Panel header menu can stack beside a chosen panel and persist without replacing controls", () =>
            {
                window.Width = 1600; window.Height = 900;
                Call(window, "RestoreDockLayout", initial); window.UpdateLayout(); PumpFor(80);
                var originals = manager.Layout.Descendents().OfType<LayoutAnchorable>().ToDictionary(panel => panel.ContentId, panel => panel.Content);
                var title = Descendants(manager).OfType<AnchorablePaneTitle>().First(header => header.Model?.ContentId == "sources");
                Check(title.ContextMenu is not null, "Panel header has no precise placement menu");
                var menu = title.ContextMenu!;
                try
                {
                    menu.PlacementTarget = title; menu.IsOpen = true; PumpFor(50);
                    var below = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Colocar debajo de");
                    below.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Biblia").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                }
                finally { menu.IsOpen = false; }
                window.UpdateLayout(); PumpFor(80);
                var source = manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(panel => panel.ContentId == "sources");
                var bible = manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(panel => panel.ContentId == "bible");
                var sourceBounds = BoundsWithin((FrameworkElement)source.Content, manager);
                var bibleBounds = BoundsWithin((FrameworkElement)bible.Content, manager);
                Check(sourceBounds.Top >= bibleBounds.Bottom && Math.Abs(sourceBounds.Left - bibleBounds.Left) < 2, "Below placed the panel as another full-height column");
                Call(window, "SaveDockLayout");
                var saved = (string)Call(window, "SerializeDockLayout")!;
                Call(window, "RestoreDockLayout", initial); Call(window, "RestoreDockLayout", saved); window.UpdateLayout();
                foreach (var panel in manager.Layout.Descendents().OfType<LayoutAnchorable>())
                    Check(ReferenceEquals(originals[panel.ContentId], panel.Content), "Placement recreated " + panel.ContentId);
                AssertSession();
            });
            Test("Every placement direction and native tab grouping preserve panels through restoration", () =>
            {
                foreach (var position in new[] { "top", "bottom", "left", "right", "tab" })
                {
                    Call(window, "RestoreDockLayout", initial);
                    Console.WriteLine("PLACEMENT " + position);
                    Check((bool)Call(window, "PlaceWorkspacePanel", "sources", "bible", position)!, "Placement was not saved");
                    window.UpdateLayout();
                    var source = position == "tab" ? Rect.Empty : BoundsWithin((FrameworkElement)Panel("sources").Content, manager);
                    var target = position == "tab" ? Rect.Empty : BoundsWithin((FrameworkElement)Panel("bible").Content, manager);
                    Check(position switch
                    {
                        "top" => source.Bottom <= target.Top && Math.Abs(source.Left - target.Left) < 2,
                        "bottom" => source.Top >= target.Bottom && Math.Abs(source.Left - target.Left) < 2,
                        "left" => source.Right <= target.Left && Math.Abs(source.Top - target.Top) < 2,
                        "right" => source.Left >= target.Right && Math.Abs(source.Top - target.Top) < 2,
                        _ => ReferenceEquals(Panel("sources").Parent, Panel("bible").Parent) && Panel("sources").IsSelected
                    }, "Incorrect relative rectangle for " + position);
                    Check(manager.Layout.Descendents().OfType<LayoutAnchorable>().Count() == 9, "Placement lost a panel");
                    var saved = (string)Call(window, "SerializeDockLayout")!;
                    Check(XDocument.Parse(saved).Root!.Attribute("WorkspacePreset") is null, "Custom placement still adapts as an untouched preset");
                    Call(window, "RestoreDockLayout", saved);
                    var custom = manager.Layout;
                    window.Width = 1250; window.UpdateLayout(); PumpFor(40);
                    window.Width = 1600; window.UpdateLayout(); PumpFor(40);
                    Check(ReferenceEquals(custom, manager.Layout), "Window resize discarded manual placement");
                    AssertSession();
                }
                Check((bool)Call(window, "PlaceWorkspacePanel", "sources", "bible", "top")!, "Could not split two tabs in the same pane");
                Check(!ReferenceEquals(Panel("sources").Parent, Panel("bible").Parent), "Two grouped tabs were not separated");
                var before = manager.Layout;
                Check(!(bool)Call(window, "PlaceWorkspacePanel", "sources", "sources", "top")!
                    && !(bool)Call(window, "PlaceWorkspacePanel", "sources", "absent", "top")!
                    && !(bool)Call(window, "PlaceWorkspacePanel", "sources", "bible", "absent")!
                    && ReferenceEquals(before, manager.Layout), "Invalid placement altered native layout");
            });
            Test("Relative placement works inside floating groups and returns every panel to the dock", () =>
            {
                Call(window, "RestoreDockLayout", initial);
                Panel("output").Float(); window.UpdateLayout(); PumpFor(60);
                var floatingXml = (string)Call(window, "SerializeDockLayout")!;
                System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "panel-sizing", "floating-before.xml"), floatingXml);
                Check((bool)Call(window, "PlaceWorkspacePanel", "sources", "output", "bottom")!, "Could not split a floating target"); PumpFor(80);
                Check(Panel("sources").IsFloating && Panel("output").IsFloating, "Placement did not stay in the floating group");
                Check((bool)Call(window, "PlaceWorkspacePanel", "sources", "bible", "top")!, "Could not move back from a floating group"); PumpFor(80);
                Check(!Panel("sources").IsFloating && Panel("output").IsFloating, "Moving back undocked an unrelated panel");
                Check((bool)Call(window, "PlaceWorkspacePanel", "output", "preview", "right")!, "Could not return the last floating panel"); PumpFor(80);
                Check(!manager.FloatingWindows.Any() && manager.Layout.Descendents().OfType<LayoutAnchorable>().Count() == 9, "Empty floating window or missing content remained");
                AssertSession();
            });
            Test("Moving and stacking the output keeps the active video playing from its current position", () =>
            {
                Call(window, "RestoreDockLayout", initial);
                Call(window, "CloseOutput");
                ((Slider)window.FindName("VolumeSlider")).Value = 0;
                ((CheckBox)window.FindName("LoopCheck")).IsChecked = true;
                Call(window, "PrepareCue", new Cue { Title = "Video de prueba", Kind = CueKind.Video,
                    FilePath = System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
                Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                try { Until(() => output.IsPlaying && !output.IsTransitioning, "Fixture video did not load"); }
                catch
                {
                    Console.WriteLine($"VIDEO status={((TextBlock)window.FindName("StatusText")).Text} playing={output.IsPlaying} paused={output.IsPaused} transition={output.IsTransitioning} live={session.Live?.Kind} blackout={session.IsBlackout} hidden={session.IsTextHidden} duration={output.Duration}");
                    throw;
                }
                var search = (TextBox)window.FindName("LibrarySearch");
                search.Text = "014";
                foreach (var id in new[] { "sources", "bible", "youtube", "media", "text", "sources" })
                { Call(window, "ActivateWorkspacePanel", id); window.UpdateLayout(); }
                Check(search.Text == "014" && output.IsPlaying, "Switching source tabs lost the search or interrupted playback");
                output.Seek(.2); PumpFor(150);
                var position = output.Position; var current = session.Live;
                var player = Field<LibVLCSharp.Shared.MediaPlayer?>(output, "_player");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                Check((bool)Call(window, "PlaceWorkspacePanel", "output", "preview", "bottom")!, "Could not stack output");
                PumpFor(180);
                Check(ReferenceEquals(output, Field<ProjectionSurface>(window, "_output")) && ReferenceEquals(current, session.Live)
                    && ReferenceEquals(player, Field<LibVLCSharp.Shared.MediaPlayer?>(output, "_player")) && output.IsPlaying
                    && (output.Position >= position - .15 || output.Duration - position + output.Position <= clock.Elapsed.TotalSeconds + .5),
                    $"Placement interrupted video: before={position} after={output.Position} playing={output.IsPlaying} elapsed={clock.Elapsed.TotalSeconds}");
                Call(window, "CloseOutput");
            });
        }
        finally
        {
            window.Width = width; window.Height = height;
            Call(window, "RestoreDockLayout", initial); window.UpdateLayout();
        }
    }
}
