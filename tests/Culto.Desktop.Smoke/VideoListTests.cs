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
        if (Environment.GetCommandLineArgs().Contains("--playlist-audio")) { RunPlaylistAudioTests(window); return; }
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
        Test("Media context menu directly appends the clicked video without choosing a list",()=>
        {
            var before=session.Live;var prepared=session.Prepared;
            var menu=(ContextMenu)Call(window,"BuildMediaPlaylistMenu",external)!;
            var add=menu.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "Añadir a lista de reproducción");
            Check(add.Header?.ToString()=="Añadir a lista de reproducción","Missing media playlist action");
            menu.PlacementTarget=window;menu.IsOpen=true;menu.UpdateLayout();
            Check(!add.HasItems,"Adding to the queue still requires choosing or creating a list");
            add.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.IsOpen=false;
            Check(set.Items.Count==3 && set.Items.Last().FilePath==external.FilePath && set.Items.Last().Id!=external.Id,"Wrong file or shared identity added");
            Check(ReferenceEquals(before,session.Live) && ReferenceEquals(prepared,session.Prepared),"Adding media changed playback or preview");
            Check(new WorkspaceStore().Load().VideoCollections.Single(s=>s.Id==set.Id).Items.Count==3,"Context addition was not saved");
            Check(((MenuItem)((ContextMenu)Call(window,"BuildMediaPlaylistMenu",new Cue{Kind=CueKind.Image})!).Items[0]).IsEnabled==false,"Nonvideo can enter playlist");
            set.Items.RemoveAt(2);Call(window,"SaveWorkspace");Call(window,"RefreshVideoListItems");
        });
        Test("Empty library automatically gets one reusable queue without a name prompt",()=>
        {
            var saved=workspace.VideoCollections.ToList();
            workspace.VideoCollections.Clear();Call(window,"RefreshVideoLists",new object?[]{null});
            var menu=(ContextMenu)Call(window,"BuildMediaPlaylistMenu",external)!;
            ((MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            ((MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(workspace.VideoCollections.Count==1 && workspace.VideoCollections[0].Items.Count==2,"Queue was not automatically created and reused");
            Check(new WorkspaceStore().Load().VideoCollections.Single().Items.Count==2,"Automatic queue was not persisted");
            workspace.VideoCollections=saved;Call(window,"RefreshVideoLists",set);Call(window,"SaveWorkspace");
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
            Until(()=>session.IsBlackout && output.DisplayedBlackout && !output.IsTransitioning,"Last list video did not transition to black");
            Check(output.PresentedTransitions.Kind==workspace.Transitions.Normalize().Kind,"End of list ignored configured transition");
            Call(window,"AdvanceVideoListIfEnded");
            Check(session.IsBlackout,"Repeated end check restored the picture");
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
            Call(window,"AppendToVideoQueue",Video("Queued while waiting"));
            Check(after.IsChecked==true && Field<VideoListPlayback>(window,"_videoListPlayback").ExpectedId==external.Id,
                "Appending to the queue cancelled the explicit continuation");
            output.Seek(.9);Until(()=>session.Live?.Id==b.Id,"Explicit continuation after external video did not run");
        });
        Test("A standalone media video also transitions to black at its end",()=>
        {
            var unrelated=Video("Unrelated ending");
            Call(window,"PrepareCue",unrelated,null);Call(window,"ProjectPrepared");
            var output=Field<ProjectionSurface>(window,"_output");
            Until(()=>output.DisplayedCue?.Id==unrelated.Id && output.IsPlaying && !output.IsTransitioning,"Unrelated video did not start");
            output.Seek(.9);Until(()=>output.HasEnded,"Unrelated video did not finish");
            Call(window,"AdvanceVideoListIfEnded");
            Until(()=>session.IsBlackout && output.DisplayedBlackout && !output.IsTransitioning,"Standalone video kept its last frame");
        });
        Call(window,"CloseOutput");
        Call(window,"ActivateWorkspacePanel","playlists");window.UpdateLayout();
        CaptureDesignWindow(window,Path.Combine(Environment.CurrentDirectory,".tools","video-list.png"));
    }
}
