using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunHymnIsolationTests(MainWindow window)
    {
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
