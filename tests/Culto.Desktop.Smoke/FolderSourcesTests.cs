using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunFolderSourcesTests(MainWindow window)
    {
        var root = Path.Combine(Path.GetTempPath(), "CultoFolderSources-" + Guid.NewGuid().ToString("N"));
        var videos = Path.Combine(root, "Videos");
        var pictures = Path.Combine(root, "Pictures");
        var downloads = Path.Combine(root, "Downloads");
        Directory.CreateDirectory(Path.Combine(videos, "Nested"));
        Directory.CreateDirectory(pictures);
        Directory.CreateDirectory(downloads);

        var workspace = Field<WorkspaceData>(window, "_workspace");
        var plan = Field<ObservableCollection<Cue>>(window, "_plan");
        var view = Field<ObservableCollection<Cue>>(window, "_mediaView");
        var importedView = Field<ObservableCollection<Cue>>(window, "_libraryView");
        var paths = Field<Dictionary<int, string>>(window, "_folderPaths");
        var filter = (ListBox)window.FindName("MediaFolders");
        var importedFilter = (ListBox)window.FindName("LibraryFilter");
        var importedSearch = (TextBox)window.FindName("LibrarySearch");
        var search = (TextBox)window.FindName("MediaSearch");
        var library = (ListBox)window.FindName("MediaList");
        // Media is initially behind another source tab; its visual tree may not
        // be realized yet. Resolve the named control rather than visible descendants.
        var refresh = (Button)window.FindName("MediaRefreshButton");
        var session = Field<PresentationSession>(window, "_session");
        var oldPaths = paths.ToDictionary(pair => pair.Key, pair => pair.Value);
        var oldLibrary = workspace.Library.ToList();
        var oldPlan = plan.ToList();
        var oldFilter = filter.SelectedValue;
        var oldImportedFilter = importedFilter.SelectedIndex;
        var oldImportedQuery = importedSearch.Text;
        var oldViewMode = workspace.MediaViewMode;
        var oldFolderFilter = workspace.MediaFolderFilter;
        var oldDescending = workspace.MediaSortDescending;
        var oldQuery = search.Text;
        var oldSource = Field<int>(window, "_activeContentSource");
        var oldPrepared = session.Prepared;
        var oldLive = session.Live;
        var oldPreparedReading = Field<IReadOnlyList<Cue>?>(window, "_preparedReading");
        var oldLiveReading = Field<IReadOnlyList<Cue>?>(window, "_liveReading");
        var hadOutput = Field<ProjectionSurface?>(window, "_output") is not null;
        var imagePath = Path.Combine(pictures, "Folder welcome.png");
        var nestedVideo = Path.Combine(videos, "Nested", "013 Folder video.mp4");
        var downloadAudio = Path.Combine(downloads, "Download audio.mp3");

        void WriteImage(string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96,
                PixelFormats.Bgr32, null, new byte[] { 20, 180, 30, 255 }, 4)));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
        bool Ready() => !Field<bool>(window, "_folderLoading");
        bool HasPaths(params string[] expected) => view.Select(cue => cue.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected);
        void SelectFolder(int index, params string[] expected)
        {
            search.Clear();
            filter.SelectedValue = index.ToString();
            Until(() => Ready() && HasPaths(expected), "Folder category did not show exactly its expected files");
        }
        void CheckLibraryUnchanged() => Check(workspace.Library.Select(cue => cue.Id).SequenceEqual(oldLibrary.Select(cue => cue.Id)),
            "Browsing a Windows folder changed the imported library");

        try
        {
            // Override every Windows source before selecting any folder category.
            Call(window, "SetLibraryFolder", -1);
            filter.SelectedIndex = -1;
            paths[2] = videos; paths[3] = pictures; paths[6] = downloads;
            Call(window, "SelectContentSource", 0);
            importedSearch.Clear();
            File.WriteAllText(nestedVideo, "Synthetic discovery fixture; never played");
            WriteImage(imagePath);
            WriteImage(Path.Combine(videos, "Wrong category.png"));
            File.WriteAllText(Path.Combine(pictures, "Wrong category.mp4"), "Synthetic discovery fixture");
            File.WriteAllText(Path.Combine(videos, "Ignored.txt"), "Unsupported content");
            File.WriteAllText(downloadAudio, "Synthetic discovery fixture; never played");
            var downloadImage = Path.Combine(downloads, "Download image.png");
            WriteImage(downloadImage);
            var downloadVideo = Path.Combine(downloads, "Download video.mp4");
            File.WriteAllText(downloadVideo, "Synthetic discovery fixture; never played");
            File.WriteAllText(Path.Combine(downloads, "Ignored.pdf"), "Unsupported content");

            Test("Folder categories discover nested media and filter Windows sources without importing", () =>
            {
                SelectFolder(2, nestedVideo);
                Check(view.Single().Kind == CueKind.Video && view.Single().HymnNumber is null,
                    "Video source inferred a hymn number from a local filename");
                SelectFolder(3, imagePath);
                Check(view.Single().Kind == CueKind.Image, "Pictures source exposed non-image media");
                SelectFolder(6, downloadAudio, downloadImage, downloadVideo);
                Check(view.Select(cue => cue.Kind).ToHashSet().SetEquals([CueKind.Audio, CueKind.Image, CueKind.Video]),
                    "Downloads did not expose all supported media kinds");
                CheckLibraryUnchanged();
                Check(refresh.Visibility == Visibility.Visible,
                    "Windows folder source has no refresh action");
            });

            Test("Folder search and refresh retain file identity and selection", () =>
            {
                SelectFolder(3, imagePath);
                var cue = view.Single();
                library.SelectedItem = cue;
                search.Text = "FOLDER WELCOME";
                Check(view.Count == 1 && view[0].Id == cue.Id, "Folder search did not match its current source case-insensitively");
                Check((library.SelectedItem as Cue)?.Id == cue.Id, "Searching discarded a still-visible selection");
                search.Text = "absent-folder-query";
                Check(view.Count == 0 && ((TextBlock)window.FindName("MediaEmptyText")).Visibility == Visibility.Visible,
                    "Unmatched folder query did not show its empty state");
                search.Clear();
                library.SelectedItem = view.Single();
                Wait((Task)Call(window, "LoadLibraryFolderAsync")!);
                Check(Ready() && view.Single().Id == cue.Id && (library.SelectedItem as Cue)?.Id == cue.Id,
                    "Refreshing unchanged folder files lost their identity or selection");
                SelectFolder(2, nestedVideo);
                SelectFolder(3, imagePath);
                Check(view.Single().Id == cue.Id, "Revisiting a folder regenerated the file's cue ID");
                CheckLibraryUnchanged();
            });

            Test("Adding a folder image copies and saves a playable order cue without importing it", () =>
            {
                SelectFolder(3, imagePath);
                var source = view.Single();
                var count = plan.Count;
                library.SelectedItem = source;
                Call(window, "AddMediaToPlanClick", window, new RoutedEventArgs());
                Check(plan.Count == count + 1, "Folder image was not added to the order");
                var added = plan.Last();
                Check(!ReferenceEquals(source, added) && added.Id != source.Id && added.FilePath == imagePath,
                    "Order item does not own an independent copy of the folder cue");
                var saved = new WorkspaceStore().Load();
                Check(saved.Plan.Items.Any(cue => cue.Id == added.Id && cue.FilePath == imagePath && cue.Kind == CueKind.Image),
                    "Folder order cue was not persisted with its media path");
                Check(saved.Library.Select(cue => cue.Id).SequenceEqual(oldLibrary.Select(cue => cue.Id)),
                    "Saving a folder order cue implicitly imported it into the library");
                Call(window, "ProjectPrepared");
                Check(session.Live?.Id == added.Id, "Added folder image could not be projected from the order");
                var output = Field<ProjectionSurface>(window, "_output");
                Until(() => !output.IsTransitioning && Field<Image>(output, "_image").Source is BitmapSource { PixelWidth: 1, PixelHeight: 1 },
                    "Folder image did not decode on the projection surface");
                CheckLibraryUnchanged();
            });

            Test("Media list and thumbnail views preserve selection and prepared/live content while saving preferences", () =>
            {
                SelectFolder(6, downloadAudio, downloadImage, downloadVideo);
                var selected = view.Single(cue => cue.FilePath == downloadImage);
                library.SelectedItem = selected;
                var separate = new Cue { Title = "Keep prepared", Body = "No replacement during a view change" };
                Call(window, "PrepareCue", separate, null);
                var prepared = session.Prepared;
                var live = session.Live;
                foreach (var (buttonName, mode, template, panel) in new[]
                {
                    ("MediaListViewButton", "List", "MediaRowTemplate", "MediaRowsPanel"),
                    ("MediaTilesViewButton", "Thumbnails", "MediaTileTemplate", "MediaTilesPanel")
                })
                {
                    ((Button)window.FindName(buttonName)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.UpdateLayout();
                    Check((library.SelectedItem as Cue)?.Id == selected.Id, "Changing media view discarded the selected file");
                    Check(ReferenceEquals(session.Prepared, prepared) && ReferenceEquals(session.Live, live),
                        "Changing media view prepared or projected content");
                    Check(ReferenceEquals(library.ItemTemplate, window.FindResource(template))
                        && ReferenceEquals(library.ItemsPanel, window.FindResource(panel)), "Media view did not select its requested presentation");
                    var stored = new WorkspaceStore().Load();
                    Check(stored.MediaViewMode == mode && stored.MediaFolderFilter == 6,
                        "Media view and folder choice did not survive persistence");
                }
                var descending = workspace.MediaSortDescending;
                ((Button)window.FindName("MediaSortButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(workspace.MediaSortDescending != descending
                    && new WorkspaceStore().Load().MediaSortDescending == workspace.MediaSortDescending,
                    "Media ordering preference was not persisted");
                var expected = workspace.MediaSortDescending
                    ? view.OrderByDescending(cue => cue.Title, StringComparer.CurrentCultureIgnoreCase)
                    : view.OrderBy(cue => cue.Title, StringComparer.CurrentCultureIgnoreCase);
                Check(view.Select(cue => cue.Id).SequenceEqual(expected.Select(cue => cue.Id)), "Media rows did not follow the chosen name order");
                Check((library.SelectedItem as Cue)?.Id == selected.Id
                    && ReferenceEquals(session.Prepared, prepared) && ReferenceEquals(session.Live, live),
                    "Sorting media changed selection or presentation");
                CheckLibraryUnchanged();
            });

            Test("Dragging a media folder file to the order makes an independent persisted cue without projecting", () =>
            {
                SelectFolder(3, imagePath);
                var source = view.Single();
                var count = plan.Count;
                var live = session.Live;
                var data = (IDataObject?)Call(window, "CreateContentDragData", library, source)
                    ?? throw new InvalidOperationException("The media browser did not create drag data");
                Check((DragDropEffects)Call(window, "ContentDropEffect", data)! == DragDropEffects.Copy,
                    "The media browser is not a supported copy source");
                var transported = new DataObject("CultoStudio.ContentCue", data.GetData("CultoStudio.ContentCue", false)!, false);
                Check((DragDropEffects)Call(window, "ApplyContentDrop", transported, 0)! == DragDropEffects.Copy,
                    "Media browser drag token was not accepted by the order");
                var added = plan[0];
                Check(plan.Count == count + 1 && added.Id != source.Id && !ReferenceEquals(source, added)
                    && added.FilePath == source.FilePath && added.Kind == CueKind.Image,
                    "Media folder drop lost its file or shared the source identity");
                Check(session.Prepared?.Id == added.Id && ReferenceEquals(session.Live, live),
                    "Media folder drop did not prepare its independent order copy without projecting");
                Check(new WorkspaceStore().Load().Plan.Items.First().Id == added.Id,
                    "Dropped media file was not persisted at the selected order position");
                Call(window, "ClearContentDrag");
                CheckLibraryUnchanged();
            });

            Test("Folder refresh preserves a separately prepared cue and the live snapshot", () =>
            {
                SelectFolder(3, imagePath);
                var selectedId = view.Single().Id;
                library.SelectedItem = view.Single();
                var separateCue = new Cue { Title = "Separately prepared text", Body = "Keep this ready", Kind = CueKind.Text };
                Call(window, "PrepareCue", separateCue, null);
                var prepared = session.Prepared;
                var live = session.Live;
                void CheckPreparation()
                {
                    Check((library.SelectedItem as Cue)?.Id == selectedId, "Folder refresh lost its selected file");
                    Check(ReferenceEquals(session.Prepared, prepared) && ReferenceEquals(session.Live, live),
                        "Restoring folder selection stole separately prepared content or changed the live snapshot");
                }
                Wait((Task)Call(window, "LoadLibraryFolderAsync")!);
                CheckPreparation();
                var arrival = Path.Combine(pictures, "Watcher preserves preparation.png");
                try
                {
                    WriteImage(arrival);
                    Until(() => Ready() && HasPaths(imagePath, arrival), "Watcher did not refresh while another cue was prepared");
                    CheckPreparation();
                }
                finally { File.Delete(arrival); }
                Until(() => Ready() && HasPaths(imagePath), "Watcher preparation fixture was not removed from the source");
                CheckPreparation();
            });

            Test("Folder watcher adds and removes files while retaining saved order entries", () =>
            {
                SelectFolder(3, imagePath);
                var source = view.Single();
                Call(window, "AddToPlan", source);
                var savedId = plan.Last().Id;
                var arrival = Path.Combine(pictures, "Arrived while browsing.png");
                WriteImage(arrival);
                Until(() => Ready() && HasPaths(imagePath, arrival), "New image did not appear automatically through the folder watcher");
                Check(view.Single(cue => cue.FilePath == imagePath).Id == source.Id, "Watcher refresh changed an existing file ID");
                File.Delete(imagePath);
                Until(() => Ready() && HasPaths(arrival), "Deleted image remained in the folder source");
                Check(plan.Any(cue => cue.Id == savedId && cue.FilePath == imagePath), "Folder deletion removed its independent order entry");
                Check(new WorkspaceStore().Load().Plan.Items.Any(cue => cue.Id == savedId && cue.FilePath == imagePath),
                    "Folder deletion erased the saved order entry");
                CheckLibraryUnchanged();
                WriteImage(imagePath);
                File.Delete(arrival);
                Until(() => Ready() && HasPaths(imagePath), "Watcher did not settle after restoring the image fixture");
            });

            Test("Switching folder categories cancels obsolete results and loading state", () =>
            {
                SelectFolder(2, nestedVideo);
                var obsoleteScan = (Task)Call(window, "LoadLibraryFolderAsync")!;
                Check(!Ready(), "Folder reload did not enter its asynchronous loading state");
                filter.SelectedValue = "3";
                Wait(obsoleteScan);
                Until(() => Ready() && HasPaths(imagePath), "An obsolete video scan replaced the active Pictures source");
                var pictureScan = (Task)Call(window, "LoadLibraryFolderAsync")!;
                importedFilter.SelectedIndex = 0;
                Call(window, "RefreshLibrary");
                Wait(pictureScan);
                Check(Ready() && HasPaths(imagePath) && importedView.Select(cue => cue.Id).ToHashSet().SetEquals(oldLibrary.Select(cue => cue.Id)),
                    "Imported library selection changed media browsing or mixed its files with imported contents");
                Check(((Button)window.FindName("RefreshFolderButton")).Visibility == Visibility.Collapsed,
                    "Imported library retained a folder-only refresh action");
            });

            Test("Unavailable folders show a coherent empty state and recover on explicit refresh", () =>
            {
                filter.SelectedIndex = -1;
                Call(window, "SetLibraryFolder", -1);
                var missing = Path.Combine(root, "MissingDownloads");
                paths[6] = missing;
                filter.SelectedValue = "6";
                Until(Ready, "Unavailable folder left the source loading indefinitely");
                var empty = (TextBlock)window.FindName("MediaEmptyText");
                Check(view.Count == 0 && empty.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(empty.Text)
                    && empty.Text == Field<string?>(window, "_folderError"), "Unavailable folder did not explain its empty source");
                Check(((TextBlock)window.FindName("MediaFolderPathText")).Text == missing,
                    "Folder status did not identify the unavailable configured path");
                Directory.CreateDirectory(missing);
                var recovered = Path.Combine(missing, "Recovered image.png");
                WriteImage(recovered);
                refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => Ready() && HasPaths(recovered), "Refresh did not recover a newly available folder");
                Check(Field<string?>(window, "_folderError") is null && empty.Visibility == Visibility.Collapsed,
                    "Recovered folder retained its unavailable error");
                CheckLibraryUnchanged();
            });
        }
        finally
        {
            filter.SelectedIndex = -1;
            Call(window, "SetLibraryFolder", -1);
            plan.Clear(); foreach (var cue in oldPlan) plan.Add(cue);
            Call(window, "UpdatePlanCount");
            paths.Clear(); foreach (var pair in oldPaths) paths[pair.Key] = pair.Value;
            // Restore controls/preferences without scanning a user's Windows folders during cleanup.
            SetField(window, "_ready", false);
            filter.SelectedValue = oldFilter;
            search.Text = oldQuery;
            importedFilter.SelectedIndex = oldImportedFilter;
            importedSearch.Text = oldImportedQuery;
            SetField(window, "_ready", true);
            Call(window, "SelectContentSource", oldSource);
            workspace.MediaFolderFilter = oldFolderFilter;
            workspace.MediaSortDescending = oldDescending;
            Call(window, "SetMediaView", oldViewMode, false);
            Call(window, "UpdateMediaSortButton");
            Call(window, "RefreshMedia");
            Call(window, "RefreshLibrary");
            if (oldLive is not null) { Call(window, "PrepareCue", oldLive, oldLiveReading); Call(window, "ProjectPrepared"); }
            if (oldPrepared is not null) Call(window, "PrepareCue", oldPrepared, oldPreparedReading);
            if (!hadOutput) Call(window, "CloseOutput");
            Call(window, "SaveWorkspace");
            Directory.Delete(root, true);
        }
    }
}
