using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace Culto.Desktop;

public partial class MainWindow
{
    private string? _dragPanelId, _dropPanelId, _dropPanelSide;
    private PanelDropHint? _panelDropHint;
    private AnchorablePaneTitle? _dragPanelTitle;
    private Point _panelDragStart;
    private bool _panelDragMoved;

    private void InitializePanelDrag(AnchorablePaneTitle title)
    {
        title.PreviewMouseLeftButtonDown += (_, e) =>
        {
            // Caption buttons retain their menu; the rest is a drag handle.
            for (var node = e.OriginalSource as DependencyObject; node is not null && node != title; node = VisualTreeHelper.GetParent(node))
                if (node is ButtonBase) return;
            if (title.Model?.ContentId is not string id || e.ClickCount != 1) return;
            BeginPanelDrag(id);
            _dragPanelTitle = title;
            _panelDragStart = title.PointToScreen(e.GetPosition(title));
            title.Focusable = true; title.Focus();
            title.CaptureMouse(); e.Handled = true;
        };
        title.PreviewMouseMove += (_, e) =>
        {
            if (_dragPanelTitle != title || e.LeftButton != MouseButtonState.Pressed) return;
            var point = title.PointToScreen(e.GetPosition(title));
            if (!_panelDragMoved && Math.Abs(point.X - _panelDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(point.Y - _panelDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _panelDragMoved = true; UpdatePanelDrag(point); e.Handled = true;
        };
        title.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (_dragPanelTitle != title) return;
            e.Handled = true; FinishPanelDrag(_panelDragMoved);
        };
        title.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _dragPanelTitle == title) { e.Handled = true; FinishPanelDrag(false); }
        };
        title.LostMouseCapture += (_, _) => { if (_dragPanelTitle == title) FinishPanelDrag(false); };
    }

    private void BeginPanelDrag(string id)
    {
        FinishPanelDrag(false);
        _dragPanelId = id; _panelDragMoved = false;
    }

    private void UpdatePanelDrag(Point screenPoint)
    {
        ClearPanelDropHint();
        if (_dragPanelId is null) return;
        foreach (var pane in WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorablePane>())
        {
            var target = pane.SelectedContent;
            if (target is null || target.ContentId == _dragPanelId || target.IsFloating
                || target.Content is not FrameworkElement { IsVisible: true } body) continue;
            var point = body.PointFromScreen(screenPoint);
            var size = body.RenderSize;
            // Include the caption above the body in the top drop area.
            if (point.X < 0 || point.X > size.Width || point.Y < -45 || point.Y > size.Height) continue;
            var x = point.X / size.Width; var y = point.Y / size.Height;
            var side = "tab";
            if (Math.Min(x, 1 - x) < .25 || Math.Min(y, 1 - y) < .25)
                side = Math.Min(y, 1 - y) <= Math.Min(x, 1 - x) ? y < .5 ? "top" : "bottom" : x < .5 ? "left" : "right";
            var bounds = body.TransformToAncestor(WorkspaceDock).TransformBounds(new Rect(size));
            var rect = side switch
            {
                "top" => new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height / 2),
                "bottom" => new Rect(bounds.Left, bounds.Top + bounds.Height / 2, bounds.Width, bounds.Height / 2),
                "left" => new Rect(bounds.Left, bounds.Top, bounds.Width / 2, bounds.Height),
                "right" => new Rect(bounds.Left + bounds.Width / 2, bounds.Top, bounds.Width / 2, bounds.Height),
                _ => bounds
            };
            _dropPanelId = target.ContentId; _dropPanelSide = side;
            var layer = AdornerLayer.GetAdornerLayer(WorkspaceDock);
            if (layer is not null) { _panelDropHint = new PanelDropHint(WorkspaceDock, rect); layer.Add(_panelDropHint); }
            break;
        }
    }

    private void FinishPanelDrag(bool commit)
    {
        var source = _dragPanelId; var target = _dropPanelId; var side = _dropPanelSide;
        var title = _dragPanelTitle;
        _dragPanelTitle = null; _dragPanelId = null;
        ClearPanelDropHint();
        if (title?.IsMouseCaptured == true) title.ReleaseMouseCapture();
        if (commit && source is not null && target is not null && side is not null) PlaceWorkspacePanel(source, target, side);
    }

    private void ClearPanelDropHint()
    {
        if (_panelDropHint is not null) AdornerLayer.GetAdornerLayer(WorkspaceDock)?.Remove(_panelDropHint);
        _panelDropHint = null; _dropPanelId = _dropPanelSide = null;
    }

    private sealed class PanelDropHint(UIElement target, Rect bounds) : Adorner(target)
    {
        protected override void OnRender(DrawingContext drawing)
        {
            IsHitTestVisible = false;
            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(75, 36, 178, 126)),
                new Pen(new SolidColorBrush(Color.FromRgb(36, 178, 126)), 2), bounds, 12, 12);
        }
    }
}
