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
    private readonly ConditionalWeakTable<LayoutAnchorableTabItem, object> _roundedDockTabs = new();
    private bool _adaptPrincipalWorkspace;
    private bool _changingWorkspacePreset;
    private bool _workspaceResizeQueued;
    private string? _appliedWorkspacePreset;
    private string? _presetArrangement;
    private Brush _dockPanelBrush = Brushes.Transparent, _dockTextBrush = Brushes.White;
    private double _dockPanelRadius = 22;
    private string DockLayoutPath => Path.Combine(_store.DirectoryPath, "paneles-v2.xml");

    private void InitializeDocking()
    {
        _dockPanelBrush = (Brush)FindResource("PanelBrush");
        _dockTextBrush = (Brush)FindResource("TextBrush");
        _dockPanelRadius = ((CornerRadius)FindResource("PanelRadius")).TopLeft;
        var resources = WorkspaceDock.Resources;
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ControlAccentBrushKey] = FindResource("AccentBrush");
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ToolWindowCaptionActiveBackground] = FindResource("PanelBrush");
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ToolWindowCaptionInactiveBackground] = FindResource("PanelBrush");
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ToolWindowCaptionActiveText] = FindResource("TextBrush");
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.ToolWindowCaptionInactiveText] = FindResource("TextBrush");
        resources[AvalonDock.Themes.Arc.Themes.ResourceKeys.PanelBorderBrush] = Brushes.Transparent;
        foreach (var pane in WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorable>())
            _dockContents[pane.ContentId] = pane.Content;
        ApplyPaneMinimums();
        WorkspaceDock.LayoutUpdated += WorkspaceDockLayoutUpdated;
        Closed += (_, _) => WorkspaceDock.LayoutUpdated -= WorkspaceDockLayoutUpdated;
        _defaultDockLayout = WorkspaceLayouts.Create("principal").ToString();
        if (File.Exists(DockLayoutPath))
        {
            try { RestoreDockLayout(File.ReadAllText(DockLayoutPath)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException or ArgumentException)
            { ApplyPrincipalWorkspace(); SetStatus("Se restableció la distribución de paneles."); }
        }
        else ApplyPrincipalWorkspace();
        // The old paneles.xml remains untouched. Only an unmodified Principal
        // preset adapts to a smaller window; a saved custom workspace stays put.
        SizeChanged += WorkspaceWindowSizeChanged;
        UpdatePrincipalWorkspaceForWidth();
    }

    private void WorkspaceDockLayoutUpdated(object? sender, EventArgs e)
    {
        if (_closing || !WorkspaceDock.IsLoaded || Dispatcher.HasShutdownStarted) return;
        ApplyPaneMinimums();
        RoundDockPanes(WorkspaceDock);
        foreach (var floating in WorkspaceDock.FloatingWindows) RoundDockPanes(floating);
    }

    private void RoundDockPanes(DependencyObject root)
    {
        if (root is LayoutAnchorablePaneControl pane)
        {
            if (!_roundedDockPanes.TryGetValue(pane, out _))
            {
                _roundedDockPanes.Add(pane, new object());
                // Keep AvalonDock's own template, drag targets and content presenter.
                pane.Margin = new Thickness(4);
                pane.Background = _dockPanelBrush;
                pane.BorderThickness = new Thickness(0);
                pane.Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 2, Opacity = .14, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
                pane.SizeChanged += DockPaneOutlineChanged;
                DockPaneOutlineChanged(pane, new RoutedEventArgs());
            }
            StyleDockCaption(pane);
            return;
        }
        // Stop at each pane: media lists and their recycled rows are not scanned.
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            RoundDockPanes(VisualTreeHelper.GetChild(root, index));
    }

    private void StyleDockCaption(DependencyObject root)
    {
        if (root is AnchorablePaneTitle title)
        {
            title.FontSize = 14;
            title.FontWeight = FontWeights.SemiBold;
            title.Padding = new Thickness(14, 10, 14, 10);
            title.Background = _dockPanelBrush;
            title.Foreground = _dockTextBrush;
            title.BorderThickness = new Thickness(0);
            // Arc 5.0.1 hardcodes this margin rather than binding Padding.
            // Adjust the existing visual; retain its template and drag handlers.
            if (VisualTreeHelper.GetChildrenCount(title) > 0
                && VisualTreeHelper.GetChild(title, 0) is Border { Child: Grid header })
                header.Margin = title.Padding;
            return;
        }
        if (root is LayoutAnchorableTabItem tab)
        {
            if (!_roundedDockTabs.TryGetValue(tab, out _))
            {
                _roundedDockTabs.Add(tab, new object());
                tab.FontSize = 12;
                tab.SizeChanged += DockPaneOutlineChanged;
                DockPaneOutlineChanged(tab, new RoutedEventArgs());
            }
            return;
        }
        if (_dockContents.Values.Contains(root)) return;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            StyleDockCaption(VisualTreeHelper.GetChild(root, index));
    }

    private void DockPaneOutlineChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement pane || pane.ActualWidth <= 0 || pane.ActualHeight <= 0) return;
        var radius = _dockPanelRadius;
        pane.Clip = new RectangleGeometry(new Rect(0, 0, pane.ActualWidth, pane.ActualHeight), radius, radius);
    }

    private string SerializeDockLayout()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new XmlLayoutSerializer(WorkspaceDock).Serialize(writer);
        var xml = writer.ToString();
        if (!_adaptPrincipalWorkspace) return xml;
        var document = XDocument.Parse(xml);
        if (_presetArrangement == WorkspaceLayouts.Arrangement(document))
            document.Root!.SetAttributeValue("WorkspacePreset", _appliedWorkspacePreset == "compacta" ? "principal-compacta" : "principal");
        return document.ToString();
    }

    private void RestoreDockLayout(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 1000000 });
        var document = XDocument.Load(reader);
        if (_dockContents.ContainsKey("media")) DockLayoutMigration.AddMediaTab(document);
        if (new[] { "bible", "text", "youtube" }.All(_dockContents.ContainsKey)) WorkspaceLayouts.ExpandLegacySources(document);
        var ids = document.Descendants().Attributes("ContentId").Select(a => a.Value).ToArray();
        if (document.Root?.Name != "LayoutRoot" || ids.Length != _dockContents.Count || !ids.Order().SequenceEqual(_dockContents.Keys.Order())
            || document.Descendants().Any(e => e.Attribute("ContentId") is not null && e.Name != "LayoutAnchorable"))
            throw new InvalidOperationException("La distribución no contiene todos los paneles.");
        var preferredPreset = (string?)document.Root.Attribute("WorkspacePreset");
        document.Root.Attribute("WorkspacePreset")?.Remove();
        // AvalonDock builds its grid minimums when panes are created. Normalize
        // saved widths and heights before deserialization, including old groups.
        foreach (var group in document.Descendants().Where(e => e.Name.LocalName is "RootPanel" or "LayoutPanel" or "LayoutAnchorablePaneGroup"))
        {
            group.Attribute("DockMinWidth")?.Remove();
            group.Attribute("DockMinHeight")?.Remove();
        }
        foreach (var pane in document.Descendants("LayoutAnchorablePane"))
        {
            pane.SetAttributeValue("DockMinWidth", pane.Elements("LayoutAnchorable")
                .Select(content => WorkspaceLayouts.MinimumWidth((string?)content.Attribute("ContentId")))
                .DefaultIfEmpty(180d).Max());
            pane.SetAttributeValue("DockMinHeight", pane.Elements("LayoutAnchorable")
                .Select(content => PaneMinimumHeight((string?)content.Attribute("ContentId")))
                .DefaultIfEmpty(160d).Max());
        }
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
        _adaptPrincipalWorkspace = false;
        _appliedWorkspacePreset = null;
        _presetArrangement = null;
        ApplyPaneMinimums();
        foreach (var pane in WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorable>())
        {
            pane.Title = WorkspaceLayouts.Title(pane.ContentId);
            pane.CanClose = false; pane.CanHide = false; pane.CanAutoHide = false; pane.CanDockAsTabbedDocument = false;
            if (pane.IsHidden) pane.Show();
            if (pane.IsAutoHidden) pane.ToggleAutoHide();
        }
        WorkspaceDock.UpdateLayout(); LiveMonitor.Refresh();
        if (preferredPreset is "principal" or "principal-compacta")
            RememberPrincipalWorkspace(preferredPreset == "principal-compacta" ? "compacta" : "principal");
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
            var width = pane.Children.Max(p => WorkspaceLayouts.MinimumWidth(p.ContentId));
            // At the supported compact height, keep both dock rows in the window.
            // Secondary output controls scroll while its picture keeps its own row.
            var height = pane.Children.Max(p => PaneMinimumHeight(p.ContentId));
            if (pane.DockMinWidth != width) pane.DockMinWidth = width;
            if (pane.DockMinHeight != height) pane.DockMinHeight = height;
        }
    }
    private static double PaneMinimumHeight(string? id) => WorkspaceLayouts.MinimumHeight(id);
    private void SaveDockLayoutClick(object sender, RoutedEventArgs e)
    { if (SaveDockLayout()) SetStatus("Distribución guardada."); }
    private void RestoreLegacyWorkspaceClick(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(_store.DirectoryPath, "paneles.xml");
        if (!File.Exists(path)) { SetStatus("No hay una distribución anterior guardada."); return; }
        try
        {
            RestoreDockLayout(File.ReadAllText(path));
            if (SaveDockLayout()) SetStatus("Distribución anterior recuperada.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException or ArgumentException)
        { SetStatus("No se pudo recuperar la distribución anterior. " + error.Message, true); }
    }
    private void ResetDockLayoutClick(object sender, RoutedEventArgs e)
    { ApplyPrincipalWorkspace(); if (SaveDockLayout()) SetStatus("Distribución principal restablecida."); }
    private void WorkspacePresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string preset } || preset is not ("principal" or "compacta" or "proyeccion")) return;
        if (preset == "principal") ApplyPrincipalWorkspace(); else ApplyWorkspacePreset(preset);
        if (SaveDockLayout()) SetStatus("Distribución " + preset + " aplicada.");
    }

    private double WorkspaceWindowWidth => ActualWidth > 0 ? ActualWidth : Width;

    private void ApplyPrincipalWorkspace() => ApplyWorkspacePreset(
        WorkspaceWindowWidth >= WorkspaceLayouts.PrincipalWindowWidth ? "principal" : "compacta", adaptPrincipal: true);

    private void ApplyWorkspacePreset(string preset, bool adaptPrincipal = false)
    {
        _changingWorkspacePreset = true;
        try
        {
            RestoreDockLayout(preset == "principal" ? _defaultDockLayout : WorkspaceLayouts.Create(preset).ToString());
            _appliedWorkspacePreset = preset;
            if (adaptPrincipal) RememberPrincipalWorkspace(preset);
        }
        finally { _changingWorkspacePreset = false; }
    }

    private void RememberPrincipalWorkspace(string preset)
    {
        _appliedWorkspacePreset = preset;
        _adaptPrincipalWorkspace = true;
        _presetArrangement = WorkspaceLayouts.Arrangement(XDocument.Parse(SerializeDockLayout()));
    }

    private void WorkspaceWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged || !_adaptPrincipalWorkspace || _changingWorkspacePreset || _workspaceResizeQueued) return;
        _workspaceResizeQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _workspaceResizeQueued = false;
            if (!_closing) UpdatePrincipalWorkspaceForWidth();
        });
    }

    private void UpdatePrincipalWorkspaceForWidth()
    {
        if (!_adaptPrincipalWorkspace || _changingWorkspacePreset) return;
        if (_presetArrangement != WorkspaceLayouts.Arrangement(XDocument.Parse(SerializeDockLayout())))
        {
            _adaptPrincipalWorkspace = false;
            return;
        }
        var preset = WorkspaceWindowWidth >= WorkspaceLayouts.PrincipalWindowWidth ? "principal" : "compacta";
        if (preset != _appliedWorkspacePreset) ApplyWorkspacePreset(preset, adaptPrincipal: true);
    }
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
