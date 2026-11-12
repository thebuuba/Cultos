using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using System.Windows.Media;
using System.Runtime.CompilerServices;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly Dictionary<string, object> _dockContents = new();
    private string _defaultDockLayout = "";
    private readonly ConditionalWeakTable<LayoutAnchorablePaneControl, object> _roundedDockPanes = new();
    private string DockLayoutPath => Path.Combine(_store.DirectoryPath, "paneles.xml");

    private void InitializeDocking()
    {
        var resources = WorkspaceDock.Resources;
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ControlAccentBrushKey] = FindResource("AccentBrush");
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ToolWindowCaptionActiveBackground] = FindResource("SelectedBrush");
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ToolWindowCaptionInactiveBackground] = FindResource("PanelBrush");
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ToolWindowCaptionActiveText] = FindResource("TextBrush");
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ToolWindowCaptionInactiveText] = FindResource("MutedBrush");
        foreach (var pane in WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorable>())
            _dockContents[pane.ContentId] = pane.Content;
        ApplyPaneMinimums();
        WorkspaceDock.LayoutUpdated += (_, _) =>
        {
            ApplyPaneMinimums();
            RoundDockPanes(WorkspaceDock);
            foreach (var floating in WorkspaceDock.FloatingWindows) RoundDockPanes(floating);
        };
        _defaultDockLayout = SerializeDockLayout();
        if (File.Exists(DockLayoutPath))
        {
            try { RestoreDockLayout(File.ReadAllText(DockLayoutPath)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException or ArgumentException)
            { RestoreDockLayout(_defaultDockLayout); SetStatus("Se restableció la distribución de paneles."); }
        }
    }

    private void RoundDockPanes(DependencyObject root)
    {
        if (root is LayoutAnchorablePaneControl pane)
        {
            if (_roundedDockPanes.TryGetValue(pane, out _)) return;
            _roundedDockPanes.Add(pane, new object());
            // Keep AvalonDock's own template, drag targets and content presenter.
            pane.Margin = new Thickness(4);
            pane.Background = (System.Windows.Media.Brush)FindResource("PanelBrush");
            pane.BorderThickness = new Thickness(0);
            pane.SizeChanged += DockPaneOutlineChanged;
            DockPaneOutlineChanged(pane, new RoutedEventArgs());
            return;
        }
        // Stop at each pane: media lists and their recycled rows are not scanned.
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            RoundDockPanes(VisualTreeHelper.GetChild(root, index));
    }

    private void DockPaneOutlineChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement pane || pane.ActualWidth <= 0 || pane.ActualHeight <= 0) return;
        var radius = ((CornerRadius)FindResource("PanelRadius")).TopLeft;
        pane.Clip = new RectangleGeometry(new Rect(0, 0, pane.ActualWidth, pane.ActualHeight), radius, radius);
    }

    private string SerializeDockLayout()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new XmlLayoutSerializer(WorkspaceDock).Serialize(writer);
        return writer.ToString();
    }

    private void RestoreDockLayout(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 1000000 });
        var document = XDocument.Load(reader);
        if (_dockContents.ContainsKey("media")) DockLayoutMigration.AddMediaTab(document);
        var ids = document.Descendants().Attributes("ContentId").Select(a => a.Value).ToArray();
        if (ids.Length != _dockContents.Count || !ids.Order().SequenceEqual(_dockContents.Keys.Order()))
            throw new InvalidOperationException("La distribución no contiene todos los paneles.");
        // AvalonDock builds its grid minimums when panes are created. Normalize
        // saved heights before deserialization so older layouts fit at 680 DIP too.
        foreach (var pane in document.Descendants("LayoutAnchorablePane"))
            pane.SetAttributeValue("DockMinHeight", pane.Elements("LayoutAnchorable")
                .Select(content => PaneMinimumHeight((string?)content.Attribute("ContentId")))
                .DefaultIfEmpty(160d).Max());
        // Correct saved geometry before the serializer creates any floating HWNDs.
        foreach (var element in document.Descendants().Where(e => e.Attribute("FloatingLeft") is not null))
        {
            double Number(string key, double fallback) => double.TryParse(element.Attribute(key)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : fallback;
            void Put(string key, double value) => element.SetAttributeValue(key, value.ToString(CultureInfo.InvariantCulture));
            Put("FloatingWidth", Math.Clamp(Number("FloatingWidth", 600), 240, 4000));
            Put("FloatingHeight", Math.Clamp(Number("FloatingHeight", 450), 180, 2400));
            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            var title = new Rect(Number("FloatingLeft", -100000), Number("FloatingTop", -100000), Number("FloatingWidth", 600), 30);
            if (!screen.IntersectsWith(title)) { Put("FloatingLeft", Left + 40); Put("FloatingTop", Top + 40); }
        }
        var serializer = new XmlLayoutSerializer(WorkspaceDock);
        serializer.LayoutSerializationCallback += (_, args) =>
        {
            if (_dockContents.TryGetValue(args.Model.ContentId, out var content)) args.Content = content;
            else args.Cancel = true;
        };
        serializer.Deserialize(new StringReader(document.ToString()));
        ApplyPaneMinimums();
        foreach (var pane in WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorable>())
        {
            pane.CanClose = false; pane.CanHide = false; pane.CanAutoHide = false; pane.CanDockAsTabbedDocument = false;
            if (pane.IsHidden) pane.Show();
            if (pane.IsAutoHidden) pane.ToggleAutoHide();
        }
        WorkspaceDock.UpdateLayout(); LiveMonitor.Refresh();
    }

    private bool SaveDockLayout()
    {
        try
        {
            var path = DockLayoutPath;
            File.WriteAllText(path + ".tmp", SerializeDockLayout());
            File.Move(path + ".tmp", path, true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        { SetStatus("No se pudo guardar la distribución de paneles. " + error.Message, true); return false; }
    }
    private void ApplyPaneMinimums()
    {
        // Pane containers can inherit the library's old minimum when tabs are moved.
        // Derive it from their current contents, including restored user layouts.
        foreach (var pane in WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorablePane>())
        {
            if (pane.Children.Count == 0) continue;
            var width = pane.Children.Max(p => p.ContentId switch { "sources" => 390d, "preview" => 280d, "output" => 220d, "media" => 320d, _ => 240d });
            // At the supported compact height, keep both dock rows in the window.
            // Secondary output controls scroll while its picture keeps its own row.
            var height = pane.Children.Max(p => PaneMinimumHeight(p.ContentId));
            if (pane.DockMinWidth != width) pane.DockMinWidth = width;
            if (pane.DockMinHeight != height) pane.DockMinHeight = height;
        }
    }
    private static double PaneMinimumHeight(string? id) => id switch
    { "sources" => 400d, "preview" or "output" => 260d, "sequence" => 150d, "media" => 260d, _ => 160d };
    private void SaveDockLayoutClick(object sender, RoutedEventArgs e)
    { if (SaveDockLayout()) SetStatus("Distribución guardada."); }
    private void ResetDockLayoutClick(object sender, RoutedEventArgs e)
    { RestoreDockLayout(_defaultDockLayout); SaveDockLayout(); SetStatus("Distribución restablecida."); }
    private void ShowWorkspacePanelClick(object sender, RoutedEventArgs e)
    { if (sender is MenuItem { Tag: string id }) ActivateWorkspacePanel(id); }
    private void ActivateWorkspacePanel(string id)
    {
        var pane = WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(p => p.ContentId == id);
        if (pane is null) return;
        pane.Show(); pane.IsActive = true; pane.IsSelected = true;
        var floating = WorkspaceDock.FloatingWindows.FirstOrDefault(w => w.Model.Descendents().Contains(pane));
        if (floating is not null) floating.Activate(); else Activate();
    }
}
