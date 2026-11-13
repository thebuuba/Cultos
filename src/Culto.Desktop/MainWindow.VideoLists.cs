using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop.Dialogs;
using Microsoft.Win32;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly VideoListPlayback _videoListPlayback = new();
    private readonly ComboBox _videoLists = new() { DisplayMemberPath = "Name", Margin = new Thickness(0,0,0,10) };
    private readonly ListBox _videoListItems = new() { DisplayMemberPath = "Title" };
    private readonly CheckBox _videoListAfter = new() { Content = "Después de este video, iniciar la lista", Margin = new Thickness(0,8,0,8) };
    private SavedSet? SelectedVideoList => _videoLists.SelectedItem as SavedSet;
    private ContextMenu BuildMediaPlaylistMenu(Cue cue)
    {
        var menu = new ContextMenu();
        var add = new MenuItem { Header = "Añadir a lista de reproducción", IsEnabled = cue.Kind == CueKind.Video };
        menu.Items.Add(add);
        void Append(SavedSet list)
        {
            if (!CheckMedia(cue)) return;
            var copy = CopyCue(cue,true); list.Items.Add(copy);
            if (!SaveWorkspace()) { list.Items.Remove(copy); return; }
            RefreshVideoLists(list); SetStatus("Añadido a " + list.Name + ": " + cue.Title);
        }
        foreach (var list in _workspace.VideoCollections)
        {
            var item = new MenuItem { Header = list.Name };
            item.Click += (_,_)=>Append(list); add.Items.Add(item);
        }
        if (add.Items.Count > 0) add.Items.Add(new Separator());
        var create = new MenuItem { Header = "Nueva lista…" };
        create.Click += (_,_)=>
        {
            var name = PromptDialog.Ask(this,"Nueva lista de videos","Nombre","");
            if (string.IsNullOrWhiteSpace(name) || !CheckMedia(cue)) return;
            var list = SavedSet.Capture(name,[cue]); _workspace.VideoCollections.Add(list);
            if (!SaveWorkspace()) { _workspace.VideoCollections.Remove(list); return; }
            RefreshVideoLists(list); SetStatus("Añadido a " + list.Name + ": " + cue.Title);
        };
        add.Items.Add(create);
        return menu;
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
        Action("Nueva lista",()=>
        {
            var name = PromptDialog.Ask(this,"Nueva lista de videos","Nombre","");
            if (string.IsNullOrWhiteSpace(name)) return;
            var list = SavedSet.Capture(name,[]); _workspace.VideoCollections.Add(list);
            if (!SaveWorkspace()) { _workspace.VideoCollections.Remove(list); return; }
            RefreshVideoLists(list);
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
        _videoLists.SelectionChanged += (_,_)=> { _videoListAfter.IsChecked = false; RefreshVideoListItems(); };
        _videoListItems.SelectionChanged += (_,_)=> { if (_videoListAfter.IsChecked == true) _videoListAfter.IsChecked = false; };
        _videoListItems.MouseDoubleClick += (_,e)=> { if(e.ChangedButton==MouseButton.Left) PlaySelectedVideoListItem(); };
        _videoListItems.PreviewKeyDown += (_,e)=> { if(e.Key==Key.Delete){EditVideoList(remove:true);e.Handled=true;} else if(e.Key==Key.Enter){PlaySelectedVideoListItem();e.Handled=true;} };
        grid.Children.Add(_videoLists); Grid.SetRow(actions,1); grid.Children.Add(actions);
        Grid.SetRow(_videoListItems,2); grid.Children.Add(_videoListItems); Grid.SetRow(footer,3); grid.Children.Add(footer);
        var body = new Border { Background = (System.Windows.Media.Brush)FindResource("PanelBrush"), Padding = new Thickness(14), Child = grid };
        var source = WorkspaceDock.Layout.Descendents().OfType<LayoutAnchorable>().Single(p=>p.ContentId=="sources");
        ((LayoutAnchorablePane)source.Parent).Children.Add(new LayoutAnchorable { Title = "Lista de videos", ContentId = "playlists", Content = body, CanClose = false, CanHide = true, CanAutoHide = false });
    }
    private void RefreshVideoLists(SavedSet? selected = null)
    {
        selected ??= SelectedVideoList;
        _videoLists.ItemsSource = null; _videoLists.ItemsSource = _workspace.VideoCollections;
        _videoLists.SelectedItem = _workspace.VideoCollections.FirstOrDefault(s=>s.Id==selected?.Id) ?? _workspace.VideoCollections.FirstOrDefault();
        RefreshVideoListItems();
    }
    private void RefreshVideoListItems()
    {
        var id = (_videoListItems.SelectedItem as Cue)?.Id;
        _videoListItems.ItemsSource = null; _videoListItems.ItemsSource = SelectedVideoList?.Items;
        _videoListItems.SelectedItem = SelectedVideoList?.Items.FirstOrDefault(c=>c.Id==id) ?? SelectedVideoList?.Items.FirstOrDefault();
    }
    private void AddPlaylistFiles()
    {
        if (SelectedVideoList is not { } list) { SetStatus("Crea o selecciona una lista."); return; }
        var dialog = new OpenFileDialog { Multiselect=true, Filter="Videos|*.mkv;*.mp4;*.mov;*.webm;*.avi;*.wmv" };
        if(dialog.ShowDialog(this)!=true)return;
        var old = list.Items.ToList();
        foreach(var path in dialog.FileNames) { var cue=MediaLibrary.FromFile(path); if(cue.Kind==CueKind.Video)list.Items.Add(cue); }
        if(!SaveWorkspace())list.Items=old; RefreshVideoListItems();
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
        var next=_videoListPlayback.Complete(live.Id,_workspace.VideoCollections.FirstOrDefault(s=>s.Id==_videoListPlayback.ListId));
        if(next is null)return;
        // Clearing the one-shot option must not cancel the newly armed list.
        var listId=_videoListPlayback.ListId!.Value;
        _videoListAfter.IsChecked=false; _videoListPlayback.Start(listId,next.Id);
        if(!CheckMedia(next)){_videoListPlayback.Cancel();return;}
        PrepareCue(next); ProjectPrepared();
    }
}
