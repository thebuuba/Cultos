using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunVideoListTests(MainWindow window)
    {
        var workspace=Field<WorkspaceData>(window,"_workspace");
        var session=Field<PresentationSession>(window,"_session");
        Cue Video(string title)=>new(){Kind=CueKind.Video,Title=title,FilePath=Path.Combine(AppContext.BaseDirectory,"fixtures","test.mkv")};
        var a=Video("List A");var b=Video("List B");var external=Video("External");
        var set=new SavedSet{Name="Saved playlist",Items=[a,b]};
        workspace.VideoCollections.Add(set);Call(window,"RefreshVideoLists",set);
        ((Slider)window.FindName("VolumeSlider")).Value=0;
        var items=Field<ListBox>(window,"_videoListItems");
        var after=Field<CheckBox>(window,"_videoListAfter");
        Test("Video list appears in source tabs and supports persistent remove and reorder",()=>
        {
            Call(window,"ActivateWorkspacePanel","playlists");window.UpdateLayout();
            Check(items.IsVisible,"Playlist tab is unavailable");
            items.SelectedItem=b;Call(window,"EditVideoList",-1,false);
            Check(set.Items[0].Id==b.Id,"Reorder failed");
            Call(window,"EditVideoList",1,false);
            var disposable=Video("Remove me");set.Items.Add(disposable);Call(window,"RefreshVideoListItems");items.SelectedItem=disposable;
            Call(window,"EditVideoList",0,true);
            Check(set.Items.Count==2 && new WorkspaceStore().Load().VideoCollections.Single(s=>s.Id==set.Id).Items.Count==2,"Remove was not persisted");
        });
        Test("Media context menu adds the clicked video to the chosen playlist without projecting",()=>
        {
            var before=session.Live;var prepared=session.Prepared;
            var menu=(ContextMenu)Call(window,"BuildMediaPlaylistMenu",external)!;
            var add=menu.Items.OfType<MenuItem>().Single();
            Check(add.Header?.ToString()=="Añadir a lista de reproducción","Missing media playlist action");
            menu.PlacementTarget=window;menu.IsOpen=true;menu.UpdateLayout();
            add.IsSubmenuOpen=true;PumpFor(150);add.ApplyTemplate();
            Check(add.Template.FindName("PART_Popup",add) is System.Windows.Controls.Primitives.Popup {IsOpen:true},"Playlist submenu has no visible popup");
            var choice=add.Items.OfType<MenuItem>().Single(m=>m.Header?.ToString()==set.Name);
            Check(choice.IsVisible && choice.ActualWidth>0,"Playlist choice is hidden");
            choice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.IsOpen=false;
            Check(set.Items.Count==3 && set.Items.Last().FilePath==external.FilePath && set.Items.Last().Id!=external.Id,"Wrong file or shared identity added");
            Check(ReferenceEquals(before,session.Live) && ReferenceEquals(prepared,session.Prepared),"Adding media changed playback or preview");
            Check(new WorkspaceStore().Load().VideoCollections.Single(s=>s.Id==set.Id).Items.Count==3,"Context addition was not saved");
            Check(((MenuItem)((ContextMenu)Call(window,"BuildMediaPlaylistMenu",new Cue{Kind=CueKind.Image})!).Items[0]).IsEnabled==false,"Nonvideo can enter playlist");
            set.Items.RemoveAt(2);Call(window,"SaveWorkspace");Call(window,"RefreshVideoListItems");
        });
        Test("Playlist advances once at natural video end and stops at its last video",()=>
        {
            items.SelectedItem=a;Call(window,"PlaySelectedVideoListItem");
            var output=Field<ProjectionSurface>(window,"_output");
            Until(()=>output.IsPlaying && !output.IsTransitioning,"List A did not start");
            output.Seek(.9);
            Until(()=>session.Live?.Id==b.Id && output.IsPlaying && !output.IsTransitioning,"List did not advance to B");
            output.Seek(.9);Until(()=>output.HasEnded,"List B did not end");
            Call(window,"AdvanceVideoListIfEnded");Check(session.Live?.Id==b.Id,"List restarted after its last video");
        });
        Test("External projection cancels the list until explicitly enabled for its ending",()=>
        {
            items.SelectedItem=a;Call(window,"PlaySelectedVideoListItem");
            Call(window,"PrepareCue",external,null);Call(window,"ProjectPrepared");
            var output=Field<ProjectionSurface>(window,"_output");
            Until(()=>output.IsPlaying && !output.IsTransitioning && output.DisplayedCue?.Id==external.Id,"External video did not start");
            Check(Field<VideoListPlayback>(window,"_videoListPlayback").ListId is null,"External video did not cancel continuation");
            items.SelectedItem=b;after.IsChecked=true;
            Check(after.IsChecked==true,"After-current option could not arm");
            output.Seek(.9);Until(()=>session.Live?.Id==b.Id,"Explicit continuation after external video did not run");
        });
        Call(window,"CloseOutput");
        Call(window,"ActivateWorkspacePanel","playlists");window.UpdateLayout();
        CaptureDesignWindow(window,Path.Combine(Environment.CurrentDirectory,".tools","video-list.png"));
    }
}
