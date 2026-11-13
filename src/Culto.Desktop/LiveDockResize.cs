using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;

namespace Culto.Desktop;

// AvalonDock 5 normally commits a ghost separator on mouse-up. Handle its Thumb
// events before the instance handlers so the real layout follows each mouse move.
public static class LiveDockResize
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(LiveDockResize), new PropertyMetadata(false));
    public static bool GetIsEnabled(DependencyObject value) => (bool)value.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject value, bool enabled) => value.SetValue(IsEnabledProperty, enabled);
    private static readonly ConditionalWeakTable<LayoutGridResizerControl, Resize> Active = new();

    static LiveDockResize()
    {
        EventManager.RegisterClassHandler(typeof(LayoutGridResizerControl), Thumb.DragStartedEvent, new DragStartedEventHandler(Start));
        EventManager.RegisterClassHandler(typeof(LayoutGridResizerControl), Thumb.DragDeltaEvent, new DragDeltaEventHandler(Move));
        EventManager.RegisterClassHandler(typeof(LayoutGridResizerControl), Thumb.DragCompletedEvent, new DragCompletedEventHandler(Complete));
        EventManager.RegisterClassHandler(typeof(LayoutGridResizerControl), Keyboard.KeyDownEvent, new KeyEventHandler(KeyDown));
    }

    private sealed record Length(Func<GridLength> Read, Action<GridLength> Write);
    private static Length Access<T>(LayoutPositionableGroup<T> model, bool horizontal) where T : class, ILayoutElement =>
        horizontal ? new(() => model.DockWidth, value => model.DockWidth = value)
                   : new(() => model.DockHeight, value => model.DockHeight = value);
    private static Length? Access(FrameworkElement element, bool horizontal) => (element as ILayoutControl)?.Model switch
    {
        LayoutPanel model => Access(model, horizontal),
        LayoutAnchorablePane model => Access(model, horizontal),
        LayoutAnchorablePaneGroup model => Access(model, horizontal),
        LayoutDocumentPane model => Access(model, horizontal),
        LayoutDocumentPaneGroup model => Access(model, horizontal),
        _ => null
    };

    private sealed class Resize(Grid grid, FrameworkElement before, FrameworkElement after, bool horizontal, Length a, Length b)
    {
        private readonly (FrameworkElement Element, Length Length, GridLength Original)[] _siblings = grid.Children
            .OfType<FrameworkElement>().Where(element => element.IsVisible)
            .Select(element => (Element: element, Length: Access(element, horizontal)))
            .Where(item => item.Length is not null)
            .Select(item => (item.Element, item.Length!, item.Length!.Read())).ToArray();
        private bool _normalized;
        private double Size(FrameworkElement element) => horizontal
            ? grid.ColumnDefinitions[Grid.GetColumn(element)].ActualWidth
            : grid.RowDefinitions[Grid.GetRow(element)].ActualHeight;
        public void Move(double movement)
        {
            var first = Size(before);
            var second = Size(after);
            if (first <= 0 || second <= 0 || !double.IsFinite(movement)) return;
            var minA = horizontal ? grid.ColumnDefinitions[Grid.GetColumn(before)].MinWidth : grid.RowDefinitions[Grid.GetRow(before)].MinHeight;
            var minB = horizontal ? grid.ColumnDefinitions[Grid.GetColumn(after)].MinWidth : grid.RowDefinitions[Grid.GetRow(after)].MinHeight;
            var delta = Math.Clamp(movement, Math.Min(0, minA - first), Math.Max(0, second - minB));
            if (Math.Abs(delta) < .001) return;
            if (!_normalized)
            {
                // A group at its minimum can be much wider than its star weight.
                // Normalize the whole row so this pair trades actual space only.
                var measured = _siblings.Select(item => (item.Length, Size: Size(item.Element))).ToArray();
                foreach (var item in measured)
                    if (item.Length.Read().IsStar) item.Length.Write(new GridLength(Math.Max(.0001, item.Size), GridUnitType.Star));
                _normalized = true;
            }
            static GridLength Adjust(GridLength length, double actual, double change) =>
                new(Math.Max(.0001, actual + change), length.IsStar ? GridUnitType.Star : GridUnitType.Pixel);
            var nextA = Adjust(a.Read(), first, delta); var nextB = Adjust(b.Read(), second, -delta);
            a.Write(nextA); b.Write(nextB);
            // Update the native video bounds too; the next Thumb delta is relative
            // to the separator's new position, avoiding accumulation or trailing motion.
            grid.UpdateLayout();
        }
        public bool Horizontal => horizontal;
        public void Cancel()
        {
            foreach (var item in _siblings) item.Length.Write(item.Original);
            grid.UpdateLayout();
        }
    }

    private static void Start(object sender, DragStartedEventArgs e)
    {
        if (sender is not LayoutGridResizerControl splitter || splitter.Parent is not Grid grid
            || grid is not ILayoutControl layout || layout.Model.Root?.Manager is not DockingManager manager || !GetIsEnabled(manager)) return;
        var horizontal = grid.ColumnDefinitions.Count > 1;
        var index = grid.Children.IndexOf(splitter);
        var siblings = grid.Children.Cast<UIElement>().ToArray();
        var before = siblings.Take(index).OfType<FrameworkElement>().LastOrDefault(c => c is ILayoutControl && c.IsVisible);
        var after = siblings.Skip(index + 1).OfType<FrameworkElement>().FirstOrDefault(c => c is ILayoutControl && c.IsVisible);
        if (before is null || after is null || Access(before, horizontal) is not Length a || Access(after, horizontal) is not Length b) return;
        Active.Remove(splitter); Active.Add(splitter, new Resize(grid, before, after, horizontal, a, b));
        splitter.Focusable = true; splitter.Focus(); e.Handled = true;
    }
    private static void Move(object sender, DragDeltaEventArgs e)
    {
        if (sender is LayoutGridResizerControl splitter && Active.TryGetValue(splitter, out var resize))
        { e.Handled = true; resize.Move(resize.Horizontal ? e.HorizontalChange : e.VerticalChange); }
    }
    private static void Complete(object sender, DragCompletedEventArgs e)
    {
        if (sender is LayoutGridResizerControl splitter && Active.TryGetValue(splitter, out var resize))
        { e.Handled = true; Active.Remove(splitter); if (e.Canceled) resize.Cancel(); }
    }
    private static void KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && sender is LayoutGridResizerControl splitter && Active.TryGetValue(splitter, out _))
        { e.Handled = true; splitter.CancelDrag(); }
    }
}
