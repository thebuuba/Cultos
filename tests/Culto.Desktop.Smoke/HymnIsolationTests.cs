using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunHymnIsolationTests(MainWindow window)
    {
        Test("Played searches clear after five seconds and edits cancel the pending reset", () =>
        {
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var hymn = new Cue { Title = "Prueba de borrado", HymnNumber = 9876, Kind = CueKind.Text, Body = "Prueba" };
            var search = (TextBox)window.FindName("LibrarySearch");
            var media = (TextBox)window.FindName("MediaSearch");
            var list = (ListBox)window.FindName("LibraryList");
            var session = Field<PresentationSession>(window, "_session");
            workspace.Library.Add(hymn);
            try
            {
                search.Text = "9876";
                list.SelectedItem = hymn;
                Call(window, "PrepareCue", hymn, null);
                Call(window, "ProjectPrepared");
                var live = session.Live;
                PumpFor(4500);
                Check(search.Text == "9876", "Search cleared before five seconds");
                PumpFor(700);
                Check(search.Text == "" && ReferenceEquals(live, session.Live), "Search reset failed or interrupted playback");
                search.Text = "9876";
                list.SelectedItem = hymn;
                Call(window, "SchedulePlayedSearchReset", hymn);
                search.Text = "otra";
                search.Text = "9876";
                PumpFor(5200);
                Check(search.Text == "9876", "A new query was cleared by the old playback timer");
                media.Text = "otro medio";
                list.SelectedItem = hymn;
                Call(window, "SchedulePlayedSearchReset", hymn);
                media.Text = "nueva busqueda";
                PumpFor(5200);
                Check(search.Text == "" && media.Text == "nueva busqueda", "Playback reset touched a different source search");
                var folders = Field<IReadOnlyList<Cue>>(window, "_folderItems");
                var mediaCue = new Cue { Title = "medio sintetico", Kind = CueKind.Text, Body = "Medio" };
                try
                {
                    SetField(window, "_folderItems", new[] { mediaCue });
                    media.Text = "medio";
                    ((ListBox)window.FindName("MediaList")).SelectedItem = mediaCue;
                    Call(window, "PrepareCue", mediaCue, null);
                    Call(window, "ProjectPrepared");
                    PumpFor(5200);
                    Check(media.Text == "", "Media playback did not reset its search");
                    media.Text = "medio";
                    ((ListBox)window.FindName("MediaList")).SelectedItem = mediaCue;
                    Call(window, "SchedulePlayedSearchReset", mediaCue);
                    media.Text = "otra";
                    PumpFor(5200);
                    Check(media.Text == "otra", "Media playback cleared a new query");
                }
                finally { SetField(window, "_folderItems", folders); }
            }
            finally
            {
                Call(window, "CancelSearchReset", search);
                Call(window, "CancelSearchReset", media);
                workspace.Library.Remove(hymn);
                search.Clear(); media.Clear();
            }
        });
        Test("Hymnal excludes ordinary media, presentations and generic text without deleting library content", () =>
        {
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var original = workspace.Library.ToArray();
            var search = (TextBox)window.FindName("LibrarySearch");
            var filters = (ListBox)window.FindName("LibraryFilter");
            var list = (ListBox)window.FindName("LibraryList");
            try
            {
                workspace.Library.Clear();
                var hymn = new Cue { Title = "001 Himno", HymnNumber = 1, Kind = CueKind.Video };
                var audio = new Cue { Title = "002 Himno audio", HymnNumber = 2, Kind = CueKind.Audio };
                workspace.Library.AddRange(new[] { hymn, audio,
                    new Cue { Title = "Bienvenidos", Kind = CueKind.Text },
                    new Cue { Title = "Despedida", Kind = CueKind.Text },
                    new Cue { Title = "Presentación", Kind = CueKind.PowerPoint, HymnNumber = 3 },
                    new Cue { Title = "Canto tema", Kind = CueKind.Video } });
                search.Clear(); filters.SelectedIndex = 0; Call(window, "RefreshLibrary");
                Check(list.Items.Cast<Cue>().Select(c => c.Id).SequenceEqual(new[] { hymn.Id, audio.Id }), "All filter mixes non-hymnal content into hymnal");
                filters.SelectedIndex = 2; Call(window, "RefreshLibrary");
                Check(list.Items.Count == 1 && ReferenceEquals(list.Items[0], audio), "Audio filter includes unrelated media");
                Check(workspace.Library.Count == 6, "Filtering deleted content from shared storage");
            }
            finally { workspace.Library.Clear(); workspace.Library.AddRange(original); search.Clear(); filters.SelectedIndex = 0; Call(window, "RefreshLibrary"); }
        });
    }
}
