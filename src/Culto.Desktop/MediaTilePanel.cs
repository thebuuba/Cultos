using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Culto.Core;

namespace Culto.Desktop;

// A fixed-height gallery with a bounded set of realized/recycled ListBoxItem
// containers. Pixel scrolling keeps layout independent of the number of files.
public sealed class MediaTilePanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(nameof(ItemWidth), typeof(double),
        typeof(MediaTilePanel), new FrameworkPropertyMetadata(184d, FrameworkPropertyMetadataOptions.AffectsMeasure), Positive);
    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(nameof(ItemHeight), typeof(double),
        typeof(MediaTilePanel), new FrameworkPropertyMetadata(148d, FrameworkPropertyMetadataOptions.AffectsMeasure), Positive);
    private TileViewport _view;
    private double _width, _offset;
    private int _count;
    public double ItemWidth { get => (double)GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }
    public double ItemHeight { get => (double)GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }
    public int Columns => Math.Max(1, _view.Columns);
    private static bool Positive(object value) => value is double number && double.IsFinite(number) && number > 0;

    public MediaTilePanel() => ClipToBounds = true;

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null) return new Size();
        _count = owner.Items.Count;
        _width = FiniteDimension(availableSize.Width, ScrollOwner?.ViewportWidth, ActualWidth, ItemWidth);
        var height = FiniteDimension(availableSize.Height, ScrollOwner?.ViewportHeight, ActualHeight, ItemHeight * 3);
        var next = TileViewport.Calculate(_count, _width, height, ItemWidth, ItemHeight, _offset);
        var changed = next != _view;
        _view = next; _offset = next.Offset;
        if (changed) ScrollOwner?.InvalidateScrollInfo();

        // Accessing InternalChildren establishes the ItemsHost generator on the
        // first measure; reading ItemContainerGenerator first can return null.
        var children = InternalChildren;
        var generator = ItemContainerGenerator;
        // Remove from the end so generator positions remain aligned with children.
        for (var childIndex = children.Count - 1; childIndex >= 0; childIndex--)
        {
            var position = new GeneratorPosition(childIndex, 0);
            var itemIndex = generator.IndexFromGeneratorPosition(position);
            if (itemIndex >= next.FirstIndex && itemIndex < next.EndIndex) continue;
            if (InternalChildren[childIndex].IsKeyboardFocusWithin && itemIndex >= 0) continue;
            if (generator is IRecyclingItemContainerGenerator recycling)
                recycling.Recycle(position, 1);
            else generator.Remove(position, 1);
            RemoveInternalChildRange(childIndex, 1);
        }

        if (next.FirstIndex < next.EndIndex)
        {
            var start = generator.GeneratorPositionFromIndex(next.FirstIndex);
            var childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
            using (generator.StartAt(start, GeneratorDirection.Forward, true))
            {
                for (var index = next.FirstIndex; index < next.EndIndex; index++, childIndex++)
                {
                    var child = (UIElement)generator.GenerateNext(out var newlyRealized);
                    if (newlyRealized || !InternalChildren.Contains(child))
                    {
                        InsertInternalChild(childIndex, child);
                        generator.PrepareItemContainer(child);
                    }
                    child.Measure(new Size(next.CellWidth, ItemHeight));
                }
            }
        }
        return new Size(_width, height);
    }

    private static double FiniteDimension(double requested, double? viewport, double actual, double fallback)
        => double.IsFinite(requested) ? Math.Max(0, requested)
            : viewport is > 0 && double.IsFinite(viewport.Value) ? viewport.Value
            : actual > 0 && double.IsFinite(actual) ? actual : fallback;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null || _view.Columns == 0) return finalSize;
        foreach (UIElement child in InternalChildren)
        {
            var index = owner.ItemContainerGenerator.IndexFromContainer(child);
            if (index < 0) continue;
            child.Arrange(new Rect((index % _view.Columns) * _view.CellWidth,
                (index / _view.Columns) * ItemHeight - _offset, _view.CellWidth, ItemHeight));
        }
        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender, args);
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                RemoveChangedChildren(args.Position.Index, args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Move:
                RemoveChangedChildren(args.OldPosition.Index, args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Reset:
                if (InternalChildren.Count > 0) RemoveInternalChildRange(0, InternalChildren.Count);
                break;
        }
        InvalidateMeasure();
    }

    private void RemoveChangedChildren(int index, int count)
    {
        if (index >= 0 && index < InternalChildren.Count && count > 0)
            RemoveInternalChildRange(index, Math.Min(count, InternalChildren.Count - index));
    }

    protected override void BringIndexIntoView(int index)
    {
        if (_view.Columns == 0) { UpdateLayout(); if (_view.Columns == 0) return; }
        if (index < 0 || index >= _count) return;
        SetVerticalOffset(_view.OffsetForIndex(index, _count));
        UpdateLayout();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (visual is null || rectangle.IsEmpty || !IsAncestorOf(visual)) return Rect.Empty;
        DependencyObject? row = visual;
        while (row is not null && VisualTreeHelper.GetParent(row) != this) row = VisualTreeHelper.GetParent(row);
        if (row is not UIElement child) return Rect.Empty;
        var index = ItemsControl.GetItemsOwner(this)?.ItemContainerGenerator.IndexFromContainer(child) ?? -1;
        if (index < 0 || _view.Columns == 0) return Rect.Empty;
        SetVerticalOffset(_view.OffsetForIndex(index, _count));
        var bounds = new Rect((index % _view.Columns) * _view.CellWidth,
            (index / _view.Columns) * ItemHeight - _offset, _view.CellWidth, ItemHeight);
        bounds.Intersect(new Rect(0, 0, ViewportWidth, ViewportHeight));
        return bounds;
    }

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public ScrollViewer? ScrollOwner { get; set; }
    public double ExtentWidth => _width;
    public double ExtentHeight => _view.ExtentHeight;
    public double ViewportWidth => _width;
    public double ViewportHeight => _view.Height;
    public double HorizontalOffset => 0;
    public double VerticalOffset => _offset;
    public void SetHorizontalOffset(double offset) { }
    public void SetVerticalOffset(double offset)
    {
        if (double.IsNaN(offset)) return;
        offset = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        if (Math.Abs(offset - _offset) < .01) return;
        _offset = offset; InvalidateMeasure(); ScrollOwner?.InvalidateScrollInfo();
    }
    public void LineUp() => SetVerticalOffset(_offset - ItemHeight / 3);
    public void LineDown() => SetVerticalOffset(_offset + ItemHeight / 3);
    public void PageUp() => SetVerticalOffset(_offset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(_offset + ViewportHeight);
    public void MouseWheelUp() => Wheel(-1);
    public void MouseWheelDown() => Wheel(1);
    private void Wheel(int direction) => SetVerticalOffset(_offset + direction *
        (SystemParameters.WheelScrollLines < 0 ? ViewportHeight : ItemHeight / 3 * SystemParameters.WheelScrollLines));
    public void LineLeft() { }
    public void LineRight() { }
    public void PageLeft() { }
    public void PageRight() { }
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
}
