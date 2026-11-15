using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;
using Culto.Remote;

internal static partial class Program
{
    private static void RunEditableSceneTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var initial = workspace.SceneTiles.ToList();
        var panel = (Panel)window.FindName("OutputBackdropsPanel");
        var store = Field<WorkspaceStore>(window, "_store");
        var session = Field<PresentationSession>(window, "_session");
        var cue = new Cue { Kind = CueKind.Text, Title = "Escena fija", Body = "Bienvenidos" };
        var custom = new SceneTile { Id = "own-scene", Label = "Bienvenida propia", Action = "cue", Color = "#4274F4", Cue = cue };
        RemoteScene[] Scenes() => ((IReadOnlyList<RemoteScene>)Call(window, "BuildRemoteScenes")!).ToArray();
        Test("Scene CRUD updates desktop, saved workspace and phone with stable IDs", () =>
        {
            Check((bool)Call(window, "SaveSceneTile", custom)!, "Add failed");
            Check(panel.Children.OfType<Button>().Any(b => b.Tag as string == custom.Id), "Added tile missing from desktop");
            Check(Scenes().Last().Id == custom.Id && store.Load().SceneTiles.Last().Id == custom.Id, "New scene not synchronized");
            Check((bool)Call(window, "MoveSceneTile", custom.Id, -1)!, "Move failed");
            Check(Scenes()[^2].Id == custom.Id, "Phone order is different");
            var edited = custom with { Label = "Nombre cambiado", Color = "#24B27E" };
            Check((bool)Call(window, "SaveSceneTile", edited)!, "Edit failed");
            Check(Scenes().Single(s => s.Id == custom.Id).Label == edited.Label, "Phone label not updated");
            Call(window, "DuplicateSceneTile", edited);
            var duplicate = workspace.SceneTiles.Last();
            Check(duplicate.Id != edited.Id && duplicate.Cue?.Id != cue.Id, "Duplicate shares scene/content IDs");
            duplicate.Cue!.Body = "Contenido distinto";
            Check(cue.Body == "Bienvenidos", "Duplicate edits change original scene");
            Call(window, "RemoveSceneTile", duplicate.Id);
            Check(!Scenes().Any(s => s.Id == duplicate.Id), "Deleted scene remains on phone");
        });
        Test("Scene editor saves custom artwork separately from projected content", () =>
        {
            var file = Path.Combine(store.DirectoryPath, "button-art.png");
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
                new byte[] { 0, 100, 255, 255, 0, 100, 255, 255, 0, 100, 255, 255, 0, 100, 255, 255 }, 8)));
            using (var stream = File.Create(file)) png.Save(stream);
            var decorated = custom with { ImageData = SceneArtwork.Import(file) };
            var dialog = new SceneTileDialog(decorated, new[] { cue }) { Owner = window };
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                Field<TextBox>(dialog, "_label").Text = "Imagen personalizada";
                dialog.UpdateLayout(); PumpFor(150);
                var folder = Path.Combine(Environment.CurrentDirectory, ".tools", "deckboard"); Directory.CreateDirectory(folder);
                CaptureDesignWindow(dialog, Path.Combine(folder, "scene-editor.png"));
                Call(dialog, "Save");
            }));
            Check(dialog.ShowDialog() == true && dialog.Result is not null, "Editor did not save");
            var saved = dialog.Result!;
            Check(saved.ImageData == decorated.ImageData && saved.Cue?.Body == cue.Body, "Button artwork changed content");
            Check((bool)Call(window, "SaveSceneTile", saved)!, "Editor result was not persisted");
            var button = panel.Children.OfType<Button>().Single(b => b.Tag as string == custom.Id);
            window.UpdateLayout(); button.ApplyTemplate(); button.UpdateLayout();
            Check(Descendants(button).OfType<Image>().Any(), "Custom artwork is not visible");
            Check(Scenes().Single(s => s.Id == custom.Id).ImageData == saved.ImageData, "Phone artwork not synchronized");
            Check(store.Load().SceneTiles.Single(s => s.Id == custom.Id).ImageData == saved.ImageData, "Artwork was not saved");
            File.Delete(file);
        });
        Test("Fixed scene content survives preview changes; removing live tile keeps projection", () =>
        {
            Call(window, "PrepareCue", new Cue { Title = "Otro contenido", Body = "No proyectar" }, null);
            var result = (CommandResult)Call(window, "ExecuteRemoteSceneMode", custom.Id, false)!;
            Check(result.Success && session.Live?.Id == cue.Id && session.Live.Body == "Bienvenidos", "Scene follows unrelated preview");
            Check(Scenes().Single(s => s.Id == custom.Id).Active, "Fixed scene lacks live marker");
            var live = session.Live;
            Check((bool)Call(window, "RemoveSceneTile", custom.Id)!, "Delete failed");
            Check(ReferenceEquals(live, session.Live), "Deleting tile changed live output");
            Check(!((CommandResult)Call(window, "ExecuteRemoteSceneMode", custom.Id, false)!).Success, "Deleted ID can still project");
        });
        Test("Built-in scenes can be deleted and empty deck stays editable", () =>
        {
            foreach (var scene in workspace.SceneTiles.ToArray()) Call(window, "RemoveSceneTile", scene.Id);
            Check(panel.Children.Count == 0 && Scenes().Length == 0 && store.Load().SceneTiles.Count == 0, "Deleted defaults returned");
            Check(((Button)window.FindName("AddSceneButton")).IsVisible, "Cannot add scenes to an empty panel");
        });
        Call(window, "SaveSceneTiles", initial);
        Call(window, "CloseOutput");
    }

    private static int RunSceneConfigurationTests()
    {
        var folder = Path.Combine(Path.GetTempPath(), "CultoScenes-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("CULTO_DATA_DIR", folder);
        Test("Existing workspaces receive independent default scene tiles", () =>
        {
            var first = new WorkspaceData();
            var second = new WorkspaceData();
            Check(first.SceneTiles.Select(s => s.Id).SequenceEqual(new[] { "logo", "blackout", "image", "video", "youtube", "browser" }), "Default scene order changed");
            first.SceneTiles.RemoveAt(0);
            Check(second.SceneTiles.Count == 6, "Scene lists are shared between workspaces");
        });
        Test("Scene content, artwork, order and empty deck survive reload", () =>
        {
            var store = new WorkspaceStore();
            var data = store.Load();
            var custom = new SceneTile { Id = "welcome", Label = "Bienvenida", Action = "cue", Color = "#123456",
                Cue = new Cue { Kind = CueKind.Text, Title = "Bienvenida", Body = "Contenido propio" } };
            data.SceneTiles = [custom, data.SceneTiles[0]];
            store.Save(data);
            var restored = store.Load();
            Check(restored.SceneTiles.Count == 2 && restored.SceneTiles[0].Id == "welcome", "Scene order was not saved");
            Check(restored.SceneTiles[0].Cue?.Body == "Contenido propio" && restored.SceneTiles[0].Color == "#123456", "Scene configuration was lost");
            data.SceneTiles.Clear(); store.Save(data);
            Check(store.Load().SceneTiles.Count == 0, "Deleted scenes returned on reload");
        });
        Test("Button artwork is a portable thumbnail independent of its source file", () =>
        {
            var path = Path.Combine(folder, "art.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8)));
            using (var file = File.Create(path)) encoder.Save(file);
            var artwork = SceneArtwork.Import(path);
            File.Delete(path);
            Check(artwork.StartsWith("data:image/png;base64,") && SceneArtwork.Decode(artwork) is not null, "Artwork still depends on the original file");
            Check(SceneArtwork.Decode("https://example.com/image.png") is null, "Untrusted remote artwork was accepted");
        });
        return failures == 0 ? 0 : 1;
    }
}
