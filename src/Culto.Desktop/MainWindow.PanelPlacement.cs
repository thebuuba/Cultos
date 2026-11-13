using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
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
                foreach (var id in _dockContents.Keys.Where(id => id != source.ContentId))
                {
                    var target = new MenuItem { Header = WorkspaceLayouts.Title(id) };
                    target.Click += (_, _) => PlaceWorkspacePanel(source.ContentId, id, position);
                    direction.Items.Add(target);
                }
                menu.Items.Add(direction);
            }
            menu.Items.Add(new Separator());
            var floating = new MenuItem { Header = source.IsFloating ? "Acoplar" : "Flotar" };
            floating.Click += (_, _) =>
            {
                if (source.IsFloating) source.Dock(); else source.Float();
                WorkspaceDock.UpdateLayout(); SaveDockLayout();
            };
            menu.Items.Add(floating);
        };
    }

    private bool PlaceWorkspacePanel(string sourceId, string targetId, string position)
    {
        var document = XDocument.Parse(SerializeDockLayout());
        if (!WorkspaceLayouts.PlacePanel(document, sourceId, targetId, position)) return false;
        RestoreDockLayout(document.ToString());
        ActivateWorkspacePanel(sourceId);
        return SaveDockLayout();
    }
}
