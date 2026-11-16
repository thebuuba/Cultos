using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop.Dialogs;
using Microsoft.Win32;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly VideoListPlayback _videoListPlayback = new();
    private readonly ListBox _videoListItems = new() { DisplayMemberPath = "Title" };
    private readonly CheckBox _videoListAfter = new() { Content = "Después de este video, iniciar la lista", Margin = new Thickness(0,8,0,8) };
    private bool _refreshingVideoLists;
    private SavedSet? SelectedVideoList { get; set; }
    private ContextMenu BuildMediaPlaylistMenu(Cue cue)
    {
        var menu = new ContextMenu();
        var add = new MenuItem { Header = "Añadir a lista de reproducción", IsEnabled = cue.Kind == CueKind.Video };
        menu.Items.Add(add);
        add.Click += (_,_)=>AppendToVideoQueue(cue);
        if (cue.Kind is CueKind.Video or CueKind.Audio or CueKind.Image or CueKind.Bible or CueKind.Text)
        {
            var split = new MenuItem { Header = cue.Kind is CueKind.Video or CueKind.Audio
                ? "Solo audio · conservar imagen" : "Mostrar conservando audio" };
            split.Click += (_, _) => { PrepareCue(cue); ProjectPreparedPreservingOther(); };
            menu.Items.Add(split);
        }
        if (!string.IsNullOrWhiteSpace(cue.FilePath))
        {
            menu.Items.Add(new Separator());
            var location = new MenuItem { Header = "Abrir ubicación del archivo", IsEnabled = File.Exists(cue.FilePath) };
            location.Click += (_, _) => OpenContentFileLocation(cue);
            menu.Items.Add(location);
        }
        return menu;
    }
    private void OpenContentFileLocation(Cue cue)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cue.FilePath) || !File.Exists(cue.FilePath))
            { SetStatus("El archivo ya no está disponible en esa ubicación.", true); return; }
            var path = Path.GetFullPath(cue.FilePath);
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        { SetStatus("No se pudo abrir la ubicación del archivo. " + error.Message, true); }
    }
    private void InitializeVideoListsPanel()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new()); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var actions = new WrapPanel { Margin = new Thickness(0,0,0,8) };
        void Action(string label, Action click)
        {
            var button = new Button { Content = label, Padding = new Thickness(8,5,8,5), MinHeight = 30, Margin = new Thickness(0,2,4,2) };
            button.Click += (_,_)=>click(); actions.Children.Add(button);
        }
        Action("Listas guardadas",()=>
        {
            var menu = new ContextMenu();
            foreach (var saved in _workspace.VideoCollections)
            {
                var item = new MenuItem { Header = saved.Name, IsCheckable = true, IsChecked = saved.Id == SelectedVideoList?.Id };
                item.Click += (_,_)=> { _videoListAfter.IsChecked = false; RefreshVideoLists(saved); SaveWorkspace(); };
                menu.Items.Add(item);
            }
            if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = "No hay listas guardadas", IsEnabled = false });
            menu.PlacementTarget = _videoListItems; menu.IsOpen = true;
        });
        Action("+ Videos", AddPlaylistFiles);
        Action("Eliminar",()=>EditVideoList(remove:true));
        Action("↑",()=>EditVideoList(delta:-1)); Action("↓",()=>EditVideoList(delta:1));
        Action("Reproducir",PlaySelectedVideoListItem);
        var footer = new StackPanel(); footer.Children.Add(_videoListAfter);
        var hint = new TextBlock { Text = "", TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush") };
        footer.Children.Add(hint);
        _videoListAfter.Checked += (_,_)=>
        {
            if (SelectedVideoList is not { } list || _videoListItems.SelectedItem is not Cue next
                || _session.Live is not { Kind: CueKind.Video } live || list.Items.Any(c=>c.Id==live.Id)
                || _output is null || _output.HasEnded) { _videoListAfter.IsChecked = false; return; }
            _videoListPlayback.After(list.Id,live.Id,next.Id); LoopCheck.IsChecked = false;
            hint.Text = "Al terminar: " + next.Title;
        };
        _videoListAfter.Unchecked += (_,_)=> { _videoListPlayback.Cancel(); hint.Text = ""; };
        _videoListItems.SelectionChanged += (_,_)=> { if (!_refreshingVideoLists && _videoListAfter.IsChecked == true) _videoListAfter.IsChecked = false; };
        _videoListItems.MouseDoubleClick += (_,e)=> { if(e.ChangedButton==MouseButton.Left) PlaySelectedVideoListItem(); };
        _videoListItems.PreviewKeyDown += (_,e)=> { if(e.Key==Key.Delete){EditVideoList(remove:true);e.Handled=true;} else if(e.Key==Key.Enter){PlaySelectedVideoListItem();e.Handled=true;} };
        grid.Children.Add(new TextBlock { Text = "Lista de reproducci\u00f3n", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,10) }); Grid.SetRow(actions,1); grid.Children.Add(actions);
        Grid.SetRow(_videoListItems,2); grid.Children.Add(_videoListItems); Grid.SetRow(footer,3); grid.Children.Add(footer);
        var body = new Border { Background = (System.Windows.Media.Brush)FindResource("PanelBrush"), Padding = new Thickness(14), Child = grid };
        var source = WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorable>().Single(p=>p.ContentId=="sources");
        ((LayoutAnchorablePane)source.Parent).Children.Add(new LayoutAnchorable { Title = "Lista de videos", ContentId = "playlists", Content = body, CanClose = false, CanHide = true, CanAutoHide = false });
    }
    private void RefreshVideoLists(SavedSet? selected = null)
    {
        selected ??= SelectedVideoList ?? _workspace.VideoCollections.FirstOrDefault(s=>s.Id==_workspace.ActiveVideoListId);
        _refreshingVideoLists = true;
        try
        {
            SelectedVideoList = _workspace.VideoCollections.FirstOrDefault(s=>s.Id==selected?.Id) ?? _workspace.VideoCollections.FirstOrDefault();
            _workspace.ActiveVideoListId = SelectedVideoList?.Id;
            RefreshVideoListItems();
        }
        finally { _refreshingVideoLists = false; }
    }
    private bool AppendToVideoQueue(Cue cue)
    {
        if (cue.Kind != CueKind.Video || !CheckMedia(cue)) return false;
        var list = SelectedVideoList ?? _workspace.VideoCollections.FirstOrDefault(s=>s.Id==_workspace.ActiveVideoListId)
            ?? _workspace.VideoCollections.FirstOrDefault();
        var created = list is null;
        list ??= SavedSet.Capture("Lista de reproducci\u00f3n",[]);
        if (created) _workspace.VideoCollections.Add(list);
        var copy = CopyCue(cue,true); list.Items.Add(copy);
        var previousId = _workspace.ActiveVideoListId;
        _workspace.ActiveVideoListId = list.Id;
        if (!SaveWorkspace())
        {
            list.Items.Remove(copy); if(created)_workspace.VideoCollections.Remove(list);
            _workspace.ActiveVideoListId = previousId; return false;
        }
        RefreshVideoLists(list); SetStatus("A\u00f1adido a la cola: " + cue.Title); return true;
    }
    private void RefreshVideoListItems()
    {
        var refreshing = _refreshingVideoLists; _refreshingVideoLists = true;
        try
        {
            var id = (_videoListItems.SelectedItem as Cue)?.Id;
            _videoListItems.ItemsSource = null; _videoListItems.ItemsSource = SelectedVideoList?.Items;
            _videoListItems.SelectedItem = SelectedVideoList?.Items.FirstOrDefault(c=>c.Id==id) ?? SelectedVideoList?.Items.FirstOrDefault();
        }
        finally { _refreshingVideoLists = refreshing; }
    }
    private void AddPlaylistFiles()
    {
        var dialog = new OpenFileDialog { Multiselect=true, Filter="Videos|*.mkv;*.mp4;*.mov;*.webm;*.avi;*.wmv" };
        if(dialog.ShowDialog(this)!=true)return;
        foreach(var path in dialog.FileNames) { var cue=MediaLibrary.FromFile(path); if(cue.Kind==CueKind.Video) AppendToVideoQueue(cue); }
    }
    private void EditVideoList(int delta = 0, bool remove = false)
    {
        if(SelectedVideoList is not {} list || _videoListItems.SelectedItem is not Cue cue)return;
        var old=list.Items.ToList(); var index=list.Items.IndexOf(cue); var next=index+delta;
        if(remove)list.Items.Remove(cue);
        else if(next>=0 && next<list.Items.Count) {list.Items.RemoveAt(index);list.Items.Insert(next,cue);}
        if(!SaveWorkspace())list.Items=old;
        RefreshVideoListItems(); if(list.Items.Contains(cue))_videoListItems.SelectedItem=cue;
    }
    private void PlaySelectedVideoListItem()
    {
        if(SelectedVideoList is not {} list || _videoListItems.SelectedItem is not Cue {Kind:CueKind.Video} cue || !CheckMedia(cue))return;
        _videoListAfter.IsChecked=false; _videoListPlayback.Start(list.Id,cue.Id); LoopCheck.IsChecked=false;
        PrepareCue(cue); ProjectPrepared();
    }
    private void ObserveVideoListProjection()
    {
        _videoListPlayback.Observe(_session.Live?.Id);
        if(_videoListPlayback.ListId is null && _videoListAfter.IsChecked==true)_videoListAfter.IsChecked=false;
    }
    private void AdvanceVideoListIfEnded()
    {
        if(_output is null || !_output.HasEnded || _output.IsTransitioning || _session.IsBlackout || _session.IsTextHidden
            || _session.Live is not {Kind:CueKind.Video} live || _output.DisplayedCue?.Id!=live.Id)return;
        var list=_workspace.VideoCollections.FirstOrDefault(s=>s.Id==_videoListPlayback.ListId);
        var next=_videoListPlayback.Complete(live.Id,list);
        if(next is null)
        {
            _session.ShowBlackout();
            ApplyBackdropTransition();
            return;
        }
        // Clearing the one-shot option must not cancel the newly armed list.
        var listId=_videoListPlayback.ListId!.Value;
        _videoListAfter.IsChecked=false; _videoListPlayback.Start(listId,next.Id);
        if(!CheckMedia(next)){_videoListPlayback.Cancel();return;}
        PrepareCue(next); ProjectPrepared();
    }
}
