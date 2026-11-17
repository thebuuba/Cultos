using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly ConditionalWeakTable<AnchorablePaneTitle, object> _placementMenus = new();

    private void InitializePanelPlacementMenu(AnchorablePaneTitle title)
    {
        if (_placementMenus.TryGetValue(title, out _)) return;
        _placementMenus.Add(title, new object());
        title.Template = (ControlTemplate)FindResource("PanelCaptionTemplate");
        title.ApplyTemplate();
        InitializePanelDrag(title);
        var menu = new ContextMenu();
        title.ContextMenu = menu;
        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();
            if (title.Model is not LayoutAnchorable source) return;
            foreach (var (label, position) in new[] { ("Colocar encima de", "top"), ("Colocar debajo de", "bottom"),
                ("Colocar a la izquierda de", "left"), ("Colocar a la derecha de", "right"), ("Agrupar con", "tab") })
            {
                var direction = new MenuItem { Header = label };
                foreach (var id in WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorable>().Where(p => !p.IsHidden && p.ContentId != source.ContentId && p.ContentId is not ("preview" or "output")).Select(p => p.ContentId))
                {
                    var target = new MenuItem { Header = WorkspaceLayouts.Title(id) };
                    target.Click += (_, _) => PlaceWorkspacePanel(source.ContentId, id, position);
                    direction.Items.Add(target);
                }
                menu.Items.Add(direction);
            }
            menu.Items.Add(new Separator());
            var floating = new MenuItem { Header = source.IsFloating ? "Acoplar" : "Desacoplar", IsEnabled = source.CanFloat };
            floating.Click += (_, _) =>
            {
                if (source.IsFloating) source.Dock(); else source.Float();
                WorkspaceDock.UpdateLayout(); SaveDockLayout();
            };
            menu.Items.Add(floating);
            var hide = new MenuItem { Header = "Cerrar panel", IsEnabled = source.CanHide };
            hide.Click += (_, _) => SetWorkspacePanelVisible(source.ContentId, false);
            menu.Items.Add(hide);
        };
    }

    private void PanelMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var title = button.TemplatedParent as AnchorablePaneTitle;
        if (title is null && button.Tag is LayoutAnchorablePaneControl pane)
            title = DragVisualChild<AnchorablePaneTitle>(pane);
        if (title?.ContextMenu is not { } menu) return;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private bool PlaceWorkspacePanel(string sourceId, string targetId, string position)
    {
        if (sourceId is "preview" or "output" || targetId is "preview" or "output") return false;
        if (WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorable>().Any(p => (p.ContentId == sourceId || p.ContentId == targetId) && p.IsHidden)) return false;
        var document = XDocument.Parse(SerializeDockLayout());
        if (!WorkspaceLayouts.PlacePanel(document, sourceId, targetId, position)) return false;
        RestoreDockLayout(document.ToString());
        ActivateWorkspacePanel(sourceId);
        return SaveDockLayout();
    }
}
