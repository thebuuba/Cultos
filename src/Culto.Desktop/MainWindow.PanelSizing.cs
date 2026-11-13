using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void InitializePanelSizing()
    {
        foreach (var (id, content) in _dockContents)
        {
            // Native monitor thumbnails keep their own clipping and transport
            // scroll areas. Lists/editors can scroll their chrome when stacked.
            if (id is "preview" or "output" or "scenes" || content is not Border { Child: Grid body } panel) continue;
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false,
                Padding = new Thickness(0)
            };
            body.MinHeight = id switch { "bible" => 400, "sources" => 360, "media" or "order" => 180, _ => 320 };
            // Give the inner star rows a finite viewport so long lists remain
            // virtualized instead of expanding to their entire item count.
            body.SetBinding(HeightProperty, new Binding(nameof(ScrollViewer.ViewportHeight))
            { Source = scroll, Mode = BindingMode.OneWay });
            if (id == "order")
                NotesPanel.IsVisibleChanged += (_, _) => body.MinHeight = NotesPanel.IsVisible ? 310 : 180;
            panel.Child = null;
            scroll.Content = body;
            panel.Child = scroll;
        }
    }
}
