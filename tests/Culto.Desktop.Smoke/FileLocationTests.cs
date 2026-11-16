using System.IO;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunFileLocationTests(MainWindow window, string directory)
    {
        Test("Media folders include Windows Music and Documents locations", () =>
        {
            var folders = (ListBox)window.FindName("MediaFolders");
            foreach (var entry in new[] { (Id: 9, Label: "Música", Folder: Environment.SpecialFolder.MyMusic),
                (Id: 10, Label: "Documentos", Folder: Environment.SpecialFolder.MyDocuments) })
            {
                Check(folders.Items.OfType<ListBoxItem>().Any(i => Equals(i.Tag, entry.Id.ToString()) && Equals(i.Content, entry.Label)), "Folder shortcut is missing: " + entry.Label);
                Check(Culto.Desktop.Services.LocalMediaFolders.GetPath(entry.Id) == Environment.GetFolderPath(entry.Folder, Environment.SpecialFolderOption.DoNotVerify), "Wrong Windows folder for " + entry.Label);
            }
        });
        Test("File context menu offers location for every local media type and preserves existing actions", () =>
        {
            var path = Path.Combine(directory, "Archivo con espacios y tildes á.png");
            File.WriteAllText(path, "Menu-only fixture");
            foreach (var kind in new[] { CueKind.Video, CueKind.Audio, CueKind.Image, CueKind.PowerPoint, CueKind.Text })
            {
                var menu = (ContextMenu)Call(window, "BuildMediaPlaylistMenu", new Cue { Kind = kind, FilePath = path })!;
                Check(menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Abrir ubicación del archivo")).IsEnabled,
                    "Existing local file cannot be located: " + kind);
                Check(menu.Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Añadir a lista de reproducción")), "Existing playlist action was lost");
            }
            var missing = (ContextMenu)Call(window, "BuildMediaPlaylistMenu", new Cue { FilePath = path + ".missing" })!;
            Check(!missing.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Abrir ubicación del archivo")).IsEnabled, "Missing file action is enabled");
            var text = (ContextMenu)Call(window, "BuildMediaPlaylistMenu", new Cue { Body = "Texto sin archivo" })!;
            Check(!text.Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Abrir ubicación del archivo")), "Text without a file has a location action");
        });
    }
}
