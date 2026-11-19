using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunReportedPlaybackTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var session = Field<PresentationSession>(window, "_session");
        ((Slider)window.FindName("VolumeSlider")).Value = 0;
        workspace.Transitions = new TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
        Cue Video(string title) => new() { Kind = CueKind.Video, Title = title,
            FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
        Test("Video scene chooses newly prepared video over active playlist with HDMI enabled", () =>
        {
            Call(window, "OpenOutput", DisplayService.GetDisplays().Last(), false);
            var a = Video("Playlist video"); var b = Video("Replacement hymn");
            var list = new SavedSet { Name = "Regression queue", Items = [a, Video("Next")] };
            workspace.VideoCollections.Add(list); Call(window, "RefreshVideoLists", list);
            Field<ListBox>(window, "_videoListItems").SelectedItem = a;
            Call(window, "PlaySelectedVideoListItem");
            var output = Field<ProjectionSurface>(window, "_output");
            Until(() => output.DisplayedCue?.Id == a.Id && output.IsPlaying && !output.IsTransitioning, "Playlist did not start");
            Call(window, "PrepareCue", b, null);
            Call(window, "SceneMediaClick", new Button { Tag = "Video" }, new RoutedEventArgs());
            Until(() => session.Live?.Id == b.Id && output.DisplayedCue?.Id == b.Id && output.IsPlaying && !output.IsTransitioning,
                "Video scene kept the playlist instead of playing the newly prepared video");
            Check(Field<VideoListPlayback>(window, "_videoListPlayback").ListId is null, "Old queue remained armed");
        });
        Test("Returning from a logo preserving audio keeps the same video decoder and position", () =>
        {
            var video = Video("Continuous video");
            Call(window, "PrepareCue", video, null); Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            Until(() => output.IsPlaying && !output.IsTransitioning, "Video did not start");
            var decoder = output.ActiveSurface;
            PumpFor(250); var before = output.Position;
            session.ShowBackground(); SetField(window, "_preserveAudioForBackdrop", true);
            Call(window, "ApplyBackdropTransition");
            Until(() => output.HasBackgroundMedia && !output.IsTransitioning, "Logo did not retain audio");
            Call(window, "ShowSceneVideo", video);
            Until(() => !output.HasBackgroundMedia && !output.IsTransitioning && output.IsPlaying, "Video did not return");
            Check(ReferenceEquals(decoder, output.ActiveSurface) && output.Position >= before, "Returning restarted the video");
        });
        Test("Scene preview prepares content without replacing live output", () =>
        {
            var live = session.Live;
            var scene = new SceneTile { Id = "preview-regression", Action = "cue", Cue = new Cue { Title = "Preview only", Body = "Prepared scene" } };
            Call(window, "PreviewConfiguredScene", scene);
            Check(session.Prepared?.Id == scene.Cue.Id && ReferenceEquals(live, session.Live), "Preview changed live output or did not prepare scene");
            Call(window, "ProjectPrepared");
            session.ShowBlackout(); Call(window, "ApplyBackdropTransition");
            Call(window, "PreviewConfiguredScene", scene);
            Check(session.IsBlackout && ((FrameworkElement)window.FindName("PreviewHost")).Visibility == Visibility.Visible,
                "Previewing the same scene mirrored blackout instead of showing its content");
            Call(window, "OpenPreviewViewer");
            var viewer = Field<Window>(window, "_previewViewer");
            Check(viewer.IsVisible, "Expanded preview did not open"); viewer.Close();
        });
        Test("Returning to Media resets Downloads to Videos only when enabled", () =>
        {
            Call(window, "ActivateWorkspacePanel", "media");
            var folders = (ListBox)window.FindName("MediaFolders");
            folders.SelectedValue = "6";
            Call(window, "ActivateWorkspacePanel", "bible");
            Call(window, "ActivateWorkspacePanel", "media");
            Check((string?)folders.SelectedValue == "2", "Media did not return to Videos");
            ((MenuItem)window.FindName("ResetMediaFolderMenu")).IsChecked = false;
            Call(window, "ToggleResetMediaFolderClick", window, new RoutedEventArgs());
            folders.SelectedValue = "6";
            Call(window, "ActivateWorkspacePanel", "bible"); Call(window, "ActivateWorkspacePanel", "media");
            Check((string?)folders.SelectedValue == "6", "Disabled preference did not retain Downloads");
            Check(!(bool)typeof(WorkspaceData).GetProperty("ResetMediaFolderOnReturn")!.GetValue(new WorkspaceStore().Load())!, "Preference did not persist");
        });
        Call(window, "CloseOutput");
    }
}
