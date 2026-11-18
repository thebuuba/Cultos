using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private const string ContentDragFormat = "CultoStudio.ContentCue";
    private sealed record ContentDrag(ListBox Source, Cue Cue);
    private ContentDrag? _contentDropPayload;
    private string? _contentDropToken;
    private ListBox? _contentDragSource;
    private Cue? _contentDragCue;
    private ListBox? _contentMouseCapture;
    private ContentDragPreview? _contentDragPreview;
    private Point _contentDragAnchor = new(.25, .5);
    private int? _planPreviewIndex;
    private readonly Dictionary<ListBoxItem, double> _planPreviewRows = [];
    private long _lastPlanDragScroll;

    private void InitializeContentDragDrop()
    {
        foreach (var list in new[] { LibraryList, BibleResults, MediaList, YouTubeList })
        {
            list.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(ContentMouseDown), true);
            list.AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(ContentMouseMove), true);
        }
        foreach (var list in new ListBox[] { LibraryList, BibleResults, PlanList, MediaList, YouTubeList })
        {
            list.AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(ContentMouseUp), true);
            list.AddHandler(QueryContinueDragEvent, new QueryContinueDragEventHandler(ContentQueryContinueDrag), true);
            list.AddHandler(GiveFeedbackEvent, new GiveFeedbackEventHandler(ContentDragFeedback), true);
        }
        foreach (var list in new ListBox[] { PlanList })
        {
            list.AllowDrop = true;
            list.AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(PlanMouseMove), true);
            // Preview routing also covers children that handle the bubbling drag events.
            list.PreviewDragEnter += PlanDragOver;
            list.PreviewDragOver += PlanDragOver;
            list.PreviewDragLeave += PlanDragLeave;
            list.PreviewDrop += PlanDrop;
        }
        // The next mouse move can already be over another panel. Keep source handlers
        // for floating panels, and also observe the main window's input route.
        AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(ContentMouseMove), true);
        AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(ContentMouseUp), true);
        Deactivated += (_, _) => { if (Mouse.LeftButton != MouseButtonState.Pressed) ClearContentDrag(); };
        Closed += (_, _) => ClearContentDrag();
    }

    private static ListBoxItem? ContentRowAt(ListBox list, object? source)
    {
        var element = source as DependencyObject;
        while (element is FrameworkContentElement content) element = content.Parent;
        return element is null ? null : ItemsControl.ContainerFromElement(list, element) as ListBoxItem;
    }

    private void PlanMouseDown(object sender, MouseButtonEventArgs e)
    {
        _orderDragPerformed = false;
        ContentMouseDown(sender, e);
        _orderDragCue = IsOrderDragSource(_contentDragSource) ? _contentDragCue : null;
    }

    private bool IsOrderDragSource(ListBox? source) => source == PlanList;

    private static Cue? ContentCue(object? item) => item switch
    {
        Cue cue => cue,
        BibleResult result => result.Cue,
        YouTubeSource source => source.Cue,
        _ => null
    };

    private void ContentMouseDown(object sender, MouseButtonEventArgs e)
    {
        ClearContentDrag();
        if (!_ready || e.ChangedButton != MouseButton.Left || sender is not ListBox list) return;
        var row = ContentRowAt(list, e.OriginalSource);
        if (list == BibleResults)
        {
            _bibleProjectionClickTimestamp = -1;
            // Let WPF extend/toggle the selection. These gestures must not arm a drag.
            if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != 0) return;
            if (row?.DataContext is BibleResult selected && list.SelectedItems.Count > 1 && list.SelectedItems.Contains(selected))
            {
                // Keep a selected passage intact when grabbing or double-clicking
                // one of its rows. The normal ListBox press would collapse it.
                e.Handled = true;
                if (e.ClickCount == 2)
                {
                    _bibleProjectionClickTimestamp = e.Timestamp;
                    ProjectBibleResult(selected);
                    return;
                }
                if (!list.IsKeyboardFocusWithin) list.Focus();
            }
        }
        if (e.ClickCount != 1) return;
        var cue = ContentCue(row?.DataContext);
        if (cue is null) return;
        _contentDragSource = list;
        _contentDragCue = cue;
        var grab = e.GetPosition(row!);
        _contentDragAnchor = new Point(Math.Clamp(grab.X / Math.Max(1, row!.ActualWidth), 0, 1),
            Math.Clamp(grab.Y / Math.Max(1, row.ActualHeight), 0, 1));
        _dragStart = e.GetPosition(list);
        if (e.LeftButton == MouseButtonState.Pressed) CaptureContentMouse(list);
        // Do not handle the press: selection, preview and double click keep their usual behavior.
    }

    private void PlanMouseMove(object sender, MouseEventArgs e) => ContentMouseMove(sender, e);

    private void ContentMouseMove(object sender, MouseEventArgs e)
    {
        if (_contentDragSource is not ListBox list || _contentDragCue is not Cue cue) return;
        if (e.LeftButton != MouseButtonState.Pressed) { ClearContentDrag(); return; }
        var delta = e.GetPosition(list) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var data = CreateContentDragData(list, cue);
        if (data is null) { ClearContentDrag(); return; }
        _contentDragCue = null; // A nested message loop must not start a second drag.
        var moving = IsOrderDragSource(list);
        if (moving) _orderDragPerformed = true;
        e.Handled = true;
        ReleaseContentMouseCapture();
        try
        {
            ShowContentDragPreview();
            if (moving) ShowOrderInsertion(PlanList, _plan.IndexOf(cue));
            DragDrop.DoDragDrop(list, data, moving ? DragDropEffects.Move : DragDropEffects.Copy);
        }
        finally { ClearContentDrag(); }
    }

    private void ContentMouseUp(object sender, MouseButtonEventArgs e)
    {
        var cue = _contentDragCue;
        var source = _contentDragSource;
        ClearContentDrag();
        if (!_ready || cue is null || _session.Prepared?.Id == cue.Id || source is not ListBox list) return;
        var releasedCue = ContentCue(ContentRowAt(list, e.OriginalSource)?.DataContext);
        // Folder rescans replace objects while keeping each file's stable ID.
        if (releasedCue is null || cue.Id != releasedCue.Id) return;
        if (list == LibraryList || list == MediaList || list == YouTubeList) PrepareCue(releasedCue);
        else if (list == BibleResults) PrepareBibleSelection();
    }

    private void ContentQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed) { e.Action = DragAction.Cancel; ClearContentDrag(); }
    }

    private void ContentDragFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (_contentDragPreview is null) return;
        e.UseDefaultCursors = false;
        Mouse.SetCursor(e.Effects == DragDropEffects.None ? Cursors.No : Cursors.Hand);
        e.Handled = true;
    }

    private void ClearContentDrag()
    {
        _contentDragPreview?.Dispose();
        _contentDragPreview = null;
        _contentDragAnchor = new Point(.25, .5);
        Mouse.UpdateCursor();
        _contentDragSource = null;
        _contentDragCue = null;
        _orderDragCue = null;
        _contentDropPayload = null;
        _contentDropToken = null;
        ReleaseContentMouseCapture();
        ClearPlanInsertion();
    }

    private void ShowContentDragPreview()
    {
        _contentDragPreview?.Dispose();
        _contentDragPreview = _contentDropPayload is { } drag
            ? new ContentDragPreview(drag.Source, drag.Cue, IsOrderDragSource(drag.Source), _contentDragAnchor) : null;
    }

    private void ReleaseContentMouseCapture()
    {
        var captured = _contentMouseCapture;
        _contentMouseCapture = null;
        if (captured is not null && Mouse.Captured == captured) captured.ReleaseMouseCapture();
    }

    private void CaptureContentMouse(ListBox list)
    {
        if (Mouse.Capture(list, CaptureMode.SubTree)) _contentMouseCapture = list;
    }

    private DataObject? CreateContentDragData(ListBox source, Cue cue)
    {
        var valid = IsOrderDragSource(source) ? _plan.Contains(cue)
            : source == LibraryList || source == MediaList ? source.Items.Contains(cue)
            : source == YouTubeList ? source.Items.OfType<YouTubeSource>().Any(item => ReferenceEquals(item.Cue, cue))
            : source == BibleResults && source.Items.OfType<BibleResult>().Any(result => ReferenceEquals(result.Cue, cue));
        if (!valid) return null;
        if (source == BibleResults && source.SelectedItems.Count > 1
            && source.SelectedItems.OfType<BibleResult>().Any(result => ReferenceEquals(result.Cue, cue)))
            cue = SelectedBibleCue() ?? cue;
        // OLE transports a string, never WPF controls or a serialized Cue object.
        // The actual source and content stay in this window for the active gesture.
        _contentDropPayload = new ContentDrag(source, cue);
        _contentDropToken = Guid.NewGuid().ToString("N");
        return new DataObject(ContentDragFormat, _contentDropToken, false);
    }

    private ContentDrag? ReadContentDrag(IDataObject data) =>
        _contentDropToken is not null && data.GetData(ContentDragFormat, false) is string token && token == _contentDropToken
            ? _contentDropPayload : null;

    private DragDropEffects ContentDropEffect(IDataObject data)
    {
        if (ReadContentDrag(data) is not ContentDrag drag) return DragDropEffects.None;
        if (IsOrderDragSource(drag.Source)) return _plan.Contains(drag.Cue) ? DragDropEffects.Move : DragDropEffects.None;
        return drag.Source == LibraryList || drag.Source == BibleResults || drag.Source == MediaList || drag.Source == YouTubeList
            ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void PlanDragOver(object sender, DragEventArgs e)
    {
        e.Effects = ContentDropEffect(e.Data) & e.AllowedEffects;
        e.Handled = true;
        if (e.Effects == DragDropEffects.None) { ClearPlanInsertion(); return; }
        var target = (ListBox)sender;
        var point = e.GetPosition(target);
        ScrollPlanDuringDrag(target, point);
        ShowOrderInsertion(target, PlanInsertionIndex(point));
    }

    private void PlanDragLeave(object sender, DragEventArgs e) => ClearPlanInsertion();

    private int PlanInsertionIndex(Point point)
    {
        // Use layout positions, not the animated hit boxes, to avoid oscillating gaps.
        for (var index = 0; index < _plan.Count; index++)
        {
            if (PlanList.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem row) continue;
            var translation = row.RenderTransform is TranslateTransform move ? move.Y : 0;
            var top = row.TranslatePoint(new Point(), PlanList).Y - translation;
            if (point.Y < top + row.ActualHeight / 2) return index;
        }
        return _plan.Count;
    }

    private void PlanDrop(object sender, DragEventArgs e)
    {
        var effect = ContentDropEffect(e.Data) & e.AllowedEffects;
        var target = (ListBox)sender;
        var point = e.GetPosition(target);
        var index = _planPreviewIndex ?? PlanInsertionIndex(point);
        e.Effects = effect == DragDropEffects.None ? effect : ApplyContentDrop(e.Data, index);
        e.Handled = true;
        ClearPlanInsertion();
    }

    private DragDropEffects ApplyContentDrop(IDataObject data, int insertionIndex)
    {
        var effect = ContentDropEffect(data);
        if (!_ready || effect == DragDropEffects.None || ReadContentDrag(data) is not ContentDrag drag)
            return DragDropEffects.None;

        var previewed = _planPreviewIndex.HasValue;
        if (effect == DragDropEffects.Move)
        {
            foreach (var row in _planPreviewRows.Keys) row.Opacity = 1;
            _planPreviewRows.Clear();
            _planPreviewIndex = null;
        }
        else ClearPlanInsertion();
        var index = Math.Clamp(insertionIndex, 0, _plan.Count);
        Cue item;
        if (effect == DragDropEffects.Move)
        {
            item = drag.Cue;
            var from = _plan.IndexOf(item);
            if (from < index) index--;
            MovePlanItem(from, index);
            if (previewed && PlanList.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem dropped)
                dropped.RenderTransform = Transform.Identity;
        }
        else
        {
            item = CopyCue(drag.Cue, true);
            _plan.Insert(index, item);
        }
        UpdatePlanCount();
        var saved = SaveWorkspace();
        PlanList.SelectedItem = item;
        PlanList.ScrollIntoView(item);
        if (saved) SetStatus(effect == DragDropEffects.Move ? "Orden actualizado." : $"Añadido al orden: {item.Title}.");
        return effect;
    }

    private static T? DragVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (DragVisualChild<T>(child) is T descendant) return descendant;
        }
        return null;
    }

    private void ScrollPlanDuringDrag(ListBox target, Point point)
    {
        if (Environment.TickCount64 - _lastPlanDragScroll < 90 || DragVisualChild<ScrollViewer>(target) is not ScrollViewer scroll) return;
        if (point.Y < 28) scroll.LineUp();
        else if (point.Y > target.ActualHeight - 28) scroll.LineDown();
        else return;
        _lastPlanDragScroll = Environment.TickCount64;
        target.UpdateLayout();
    }

    private void ShowOrderInsertion(ListBox target, int index)
    {
        if (_contentDropPayload is not { } drag) return;
        var from = IsOrderDragSource(drag.Source) ? _plan.IndexOf(drag.Cue) : -1;
        var destination = from >= 0 && from < index ? index - 1 : index;
        var sourceRow = from >= 0 ? target.ItemContainerGenerator.ContainerFromIndex(from) as ListBoxItem : null;
        var height = sourceRow is not null ? sourceRow.ActualHeight + sourceRow.Margin.Top + sourceRow.Margin.Bottom : 38;
        _planPreviewIndex = index;
        for (var rowIndex = 0; rowIndex < _plan.Count; rowIndex++)
        {
            if (target.ItemContainerGenerator.ContainerFromIndex(rowIndex) is not ListBoxItem row) continue;
            if (!_planPreviewRows.ContainsKey(row)) _planPreviewRows.Add(row, double.NaN);
            row.Opacity = rowIndex == from ? 0 : 1;
            var offset = from < 0 ? (rowIndex >= index ? height : 0)
                : from < destination && rowIndex > from && rowIndex <= destination ? -height
                : destination < from && rowIndex >= destination && rowIndex < from ? height : 0;
            if (_planPreviewRows[row] == offset) continue;
            _planPreviewRows[row] = offset;
            var current = row.RenderTransform is TranslateTransform previous ? previous.Y : 0;
            var move = new TranslateTransform(0, offset);
            row.RenderTransform = move;
            if (SystemParameters.ClientAreaAnimation)
                move.BeginAnimation(TranslateTransform.YProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(current, offset, TimeSpan.FromMilliseconds(180))
                    { EasingFunction = new System.Windows.Media.Animation.CubicEase
                        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
        }
    }

    private void ClearPlanInsertion()
    {
        foreach (var row in _planPreviewRows.Keys)
        {
            row.Opacity = 1;
            row.RenderTransform = Transform.Identity;
        }
        _planPreviewRows.Clear();
        _planPreviewIndex = null;
    }
}
