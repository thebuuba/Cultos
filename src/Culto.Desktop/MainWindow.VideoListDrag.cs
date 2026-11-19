using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private const string VideoListMoveFormat = "CultoStudio.VideoListMove";
    private Point _videoListDragStart;
    private Cue? _videoListDragCue;
    private Guid? _videoListDragList;

    private void InitializeVideoListDrag()
    {
        _videoListItems.AllowDrop = true;
        _videoListItems.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _videoListDragCue = e.ClickCount == 1 ? ContentRowAt(_videoListItems, e.OriginalSource)?.DataContext as Cue : null;
            _videoListDragList = SelectedVideoList?.Id;
            _videoListDragStart = e.GetPosition(_videoListItems);
        };
        _videoListItems.PreviewMouseLeftButtonUp += (_, _) => _videoListDragCue = null;
        _videoListItems.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) { _videoListDragCue = null; return; }
            if (_videoListDragCue is not { } cue || _videoListDragList != SelectedVideoList?.Id) return;
            var offset = e.GetPosition(_videoListItems) - _videoListDragStart;
            if (Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            e.Handled = true;
            try
            {
                DragDrop.DoDragDrop(_videoListItems, new DataObject(VideoListMoveFormat, cue.Id.ToString()), DragDropEffects.Move);
            }
            finally { _videoListDragCue = null; _videoListDragList = null; }
        };
        _videoListItems.PreviewDragOver += (_, e) =>
        {
            e.Effects = VideoListDragEffect(e.Data) & e.AllowedEffects;
            if (e.Effects == DragDropEffects.Move) ScrollPlanDuringDrag(_videoListItems, e.GetPosition(_videoListItems));
            e.Handled = true;
        };
        _videoListItems.PreviewDrop += (_, e) =>
        {
            e.Effects = (e.AllowedEffects & DragDropEffects.Move) != 0
                && DropVideoListItem(e.Data, e.GetPosition(_videoListItems)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
    }

    private bool DropVideoListItem(IDataObject data, Point point)
    {
        if (VideoListDragEffect(data) != DragDropEffects.Move || _videoListDragCue is not { } cue || SelectedVideoList is not { } list) return false;
        var insertion = list.Items.Count;
        for (var index = 0; index < list.Items.Count; index++)
        {
            if (_videoListItems.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem row) continue;
            if (point.Y < row.TranslatePoint(new Point(), _videoListItems).Y + row.ActualHeight / 2)
            { insertion = index; break; }
            insertion = index + 1;
        }
        if (list.Items.IndexOf(cue) < insertion) insertion--;
        return MoveVideoListItem(cue, insertion);
    }

    private DragDropEffects VideoListDragEffect(IDataObject data) =>
        _videoListDragCue is { } cue && _videoListDragList == SelectedVideoList?.Id
        && SelectedVideoList?.Items.Contains(cue) == true
        && data.GetDataPresent(VideoListMoveFormat) && data.GetData(VideoListMoveFormat) as string == cue.Id.ToString()
            ? DragDropEffects.Move : DragDropEffects.None;

    private bool MoveVideoListItem(Cue cue, int destination)
    {
        if (SelectedVideoList is not { } list || !list.Items.Contains(cue) || destination < 0 || destination >= list.Items.Count) return false;
        var old = list.Items.ToList();
        var from = list.Items.IndexOf(cue);
        if (from == destination) return true;
        list.Items.RemoveAt(from); list.Items.Insert(destination, cue);
        var saved = SaveWorkspace();
        if (!saved) list.Items = old;
        RefreshVideoListItems();
        _videoListItems.SelectedItem = cue;
        _videoListItems.ScrollIntoView(cue);
        if (saved) SetStatus("Orden actualizado. Los videos seguirán el nuevo orden al terminar el actual.");
        return saved;
    }
}
