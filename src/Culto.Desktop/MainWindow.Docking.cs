using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly Dictionary<string, object> _dockContents = new();
    private string _defaultDockLayout = "";
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
        WorkspaceDock.LayoutUpdated += (_, _) => ApplyPaneMinimums();
        _defaultDockLayout = SerializeDockLayout();
        if (File.Exists(DockLayoutPath))
        {
            try { RestoreDockLayout(File.ReadAllText(DockLayoutPath)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException or ArgumentException)
            { RestoreDockLayout(_defaultDockLayout); SetStatus("Se restableció la distribución de paneles."); }
        }
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
        var ids = document.Descendants().Attributes("ContentId").Select(a => a.Value).ToArray();
        if (ids.Length != _dockContents.Count || !ids.Order().SequenceEqual(_dockContents.Keys.Order()))
            throw new InvalidOperationException("La distribución no contiene todos los paneles.");
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
            var width = pane.Children.Max(p => p.ContentId switch { "sources" => 390d, "preview" => 280d, "output" => 220d, _ => 240d });
            // The preview's timeline, time labels and transport need separate rows;
            // retain at least 80px for the picture after controls and dock chrome.
            var height = pane.Children.Max(p => p.ContentId switch { "sources" => 400d, "preview" => 340d, "output" => 280d, "sequence" => 150d, _ => 160d });
            if (pane.DockMinWidth != width) pane.DockMinWidth = width;
            if (pane.DockMinHeight != height) pane.DockMinHeight = height;
        }
    }
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
