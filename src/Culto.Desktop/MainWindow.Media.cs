using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly ObservableCollection<Cue> _mediaView = [];
    private bool _refreshingMedia;

    private void InitializeMediaBrowser()
    {
        MediaList.ItemsSource = _mediaView;
        foreach (var list in new[] { MediaList, LibraryList })
        list.PreviewMouseRightButtonUp += (_, e) =>
        {
            var row = ContentRowAt(list, e.OriginalSource);
            if (row?.DataContext is not Cue cue) return;
            var menu = BuildMediaPlaylistMenu(cue);
            menu.PlacementTarget = row;
            menu.IsOpen = true; e.Handled = true;
        };
        if (!string.IsNullOrWhiteSpace(_workspace.MediaCustomFolderPath))
        {
            _folderPaths[8] = _workspace.MediaCustomFolderPath;
        }
        SetMediaView(_workspace.MediaViewMode, save: false);
        var folder = IsFolderFilter(_workspace.MediaFolderFilter) ? _workspace.MediaFolderFilter : 2;
        if (folder == 8 && !_folderPaths.ContainsKey(8)) folder = 2;
        MediaFolders.SelectedValue = folder.ToString();
        UpdateMediaSortButton();
    }

    private void MediaFolderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || !int.TryParse(MediaFolders.SelectedValue as string, out var filter)) return;
        MediaSearch.Clear();
        _workspace.MediaFolderFilter = filter;
        SetLibraryFolder(filter);
        RefreshMedia();
        SaveWorkspace();
    }

    private void ChooseMediaFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Carpeta de medios", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        MediaSearch.Clear();
        _workspace.MediaCustomFolderPath = dialog.FolderName;
        _folderPaths[8] = dialog.FolderName;
        if (_activeFolderFilter == 8) SetLibraryFolder(-1);
        MediaFolders.SelectedValue = "8";
        SetLibraryFolder(8);
        _workspace.MediaFolderFilter = 8;
        RefreshMedia(); SaveWorkspace();
    }

    private void RefreshMedia()
    {
        if (!_ready) return;
        var query = MediaSearch.Text.Trim();
        var selectedId = (MediaList.SelectedItem as Cue)?.Id;
        var filtered = _folderItems.Where(cue => query.Length == 0
            || cue.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || Path.GetFileName(cue.FilePath)?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true);
        var sorted = _workspace.MediaSortDescending
            ? filtered.OrderByDescending(cue => cue.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(cue => cue.FilePath)
            : filtered.OrderBy(cue => cue.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(cue => cue.FilePath);
        _refreshingMedia = true;
        try
        {
            _mediaView.Clear();
            foreach (var cue in sorted) _mediaView.Add(cue);
            MediaList.SelectedItem = _mediaView.FirstOrDefault(cue => cue.Id == selectedId);
        }
        finally { _refreshingMedia = false; }
        MediaFolderPathText.Text = CurrentFolderPath;
        ChooseMediaFolderButton.ToolTip = _activeFolderFilter == 8 ? CurrentFolderPath : "Elegir carpeta";
        ChooseMediaFolderButton.Foreground = (Brush)FindResource(_activeFolderFilter == 8 ? "AccentBrush" : "MutedBrush");
        MediaCountText.Text = _folderLoading ? "Cargando…" : $"{_mediaView.Count:N0} archivos";
        MediaCountText.ToolTip = _folderError;
        MediaEmptyText.Text = _folderLoading ? "Cargando archivos…" : _folderError
            ?? (query.Length > 0 ? "Sin resultados" : "No hay archivos multimedia en esta carpeta");
        MediaEmptyText.Visibility = _mediaView.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AddMediaToPlanButton.IsEnabled = MediaList.SelectedItem is Cue;
    }

    private void MediaSearchChanged(object sender, TextChangedEventArgs e) => RefreshMedia();
    private void MediaSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (!_ready) return;
        if (e.Key == Key.Escape) { MediaSearch.Clear(); e.Handled = true; return; }
        if (e.Key is not (Key.Enter or Key.Down)) return;
        e.Handled = true;
        if (_mediaView.Count == 0) return;
        MediaList.SelectedItem ??= _mediaView[0];
        MediaList.ScrollIntoView(MediaList.SelectedItem);
        MediaList.UpdateLayout();
        if (MediaList.ItemContainerGenerator.ContainerFromItem(MediaList.SelectedItem) is ListBoxItem row) row.Focus();
    }
    private void MediaSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _refreshingMedia) return;
        AddMediaToPlanButton.IsEnabled = MediaList.SelectedItem is Cue;
        if (MediaList.SelectedItem is not Cue cue) return;
        if (_contentDragSource == MediaList && _contentDragCue is not null && Mouse.LeftButton == MouseButtonState.Pressed) return;
        PrepareCue(cue);
    }
    private void MediaDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!_ready || e.ChangedButton != MouseButton.Left || ContentRowAt(MediaList, e.OriginalSource)?.DataContext is not Cue cue) return;
        e.Handled = true;
        PrepareCue(cue); ProjectPreparedFromInput();
    }
    private void MediaListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        { MediaSearch.Focus(); MediaSearch.SelectAll(); e.Handled = true; return; }
        if (_workspace.MediaViewMode != "List" && Keyboard.Modifiers == ModifierKeys.None && MediaList.Items.Count > 0)
        {
            var panel = DragVisualChild<MediaTilePanel>(MediaList);
            var columns = Math.Max(1, panel?.Columns ?? 1);
            var current = MediaList.SelectedIndex;
            var page = columns * Math.Max(1, (int)((panel?.ViewportHeight ?? 160) / (panel?.ItemHeight ?? 160)));
            int? next = e.Key switch { Key.Left => current - 1, Key.Right => current + 1,
                Key.Up => current - columns, Key.Down => current + columns, Key.Home => 0,
                Key.End => MediaList.Items.Count - 1, Key.PageUp => current - page, Key.PageDown => current + page, _ => null };
            if (next is not null)
            {
                e.Handled = true;
                MediaList.SelectedIndex = Math.Clamp(current < 0 && e.Key != Key.End ? 0 : next.Value, 0, MediaList.Items.Count - 1);
                MediaList.ScrollIntoView(MediaList.SelectedItem); MediaList.UpdateLayout();
                if (MediaList.ItemContainerGenerator.ContainerFromItem(MediaList.SelectedItem) is ListBoxItem row) row.Focus();
                return;
            }
        }
        if (e.Key != Key.Enter || e.IsRepeat || MediaList.SelectedItem is not Cue cue) return;
        e.Handled = true; PrepareCue(cue); ProjectPreparedFromInput();
    }
    private void AddMediaToPlanClick(object sender, RoutedEventArgs e)
    { if (MediaList.SelectedItem is Cue cue) AddToPlan(cue); }

    private void MediaViewClick(object sender, RoutedEventArgs e)
    { if (sender is Button { Tag: string mode }) SetMediaView(mode, save: true); }
    private void SetMediaView(string? mode, bool save)
    {
        var thumbnails = mode != "List";
        _workspace.MediaViewMode = thumbnails ? "Thumbnails" : "List";
        MediaList.ItemsPanel = (ItemsPanelTemplate)FindResource(thumbnails ? "MediaTilesPanel" : "MediaRowsPanel");
        MediaList.ItemTemplate = (DataTemplate)FindResource(thumbnails ? "MediaTileTemplate" : "MediaRowTemplate");
        MediaList.ItemContainerStyle = (Style)FindResource(thumbnails ? "MediaTileItem" : "CompactCueRow");
        MediaTilesViewButton.Background = (Brush)FindResource(thumbnails ? "SelectedBrush" : "CanvasBrush");
        MediaTilesViewButton.Foreground = (Brush)FindResource(thumbnails ? "AccentBrush" : "MutedBrush");
        MediaListViewButton.Background = (Brush)FindResource(thumbnails ? "CanvasBrush" : "SelectedBrush");
        MediaListViewButton.Foreground = (Brush)FindResource(thumbnails ? "MutedBrush" : "AccentBrush");
        if (save) SaveWorkspace();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!_closing && MediaList.SelectedItem is { } selected) MediaList.ScrollIntoView(selected);
        });
    }
    private void MediaSortClick(object sender, RoutedEventArgs e)
    {
        _workspace.MediaSortDescending = !_workspace.MediaSortDescending;
        UpdateMediaSortButton(); RefreshMedia(); SaveWorkspace();
    }
    private void UpdateMediaSortButton() => MediaSortButton.Content = _workspace.MediaSortDescending ? "Z–A" : "A–Z";

    private void MediaPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (MediaList is null) return;
        var narrow = e.NewSize.Width < 300;
        Grid.SetColumnSpan(MediaSearch, narrow ? 4 : 1);
        Grid.SetColumnSpan(MediaSearchHint, narrow ? 4 : 1);
        foreach (var button in new[] { MediaListViewButton, MediaTilesViewButton, MediaRefreshButton })
            Grid.SetRow(button, narrow ? 1 : 0);
        var compact = e.NewSize.Height < 290;
        MediaFolderPathText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }
}
