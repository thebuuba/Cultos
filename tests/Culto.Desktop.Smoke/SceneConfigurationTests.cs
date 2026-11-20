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
        Test("Program title accepts text without artwork and preserves edits", () =>
        {
            var original = new SceneTile { Action = "program", Cue = new Cue { Kind = CueKind.Image,
                ProgramTitle = new("old-theme.png", "old-purpose.png") } };
            var dialog = new SceneTileDialog(original, []) { Owner = window };
            Exception? callbackError = null;
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    Field<TextBox>(dialog, "_programTheme").Clear();
                    Field<TextBox>(dialog, "_programPurpose").Clear();
                    Field<TextBox>(dialog, "_programThemeText").Text = "Tema del sábado";
                    Field<TextBox>(dialog, "_programPurposeText").Text = "Servir a nuestra comunidad";
                    Call(dialog, "Save");
                }
                catch (Exception error) { callbackError = error; }
                finally { if (dialog.IsVisible) dialog.DialogResult = false; }
            }));
            var saved = dialog.ShowDialog();
            if (callbackError is not null) throw callbackError;
            Check(saved == true && dialog.Result?.Cue is { Kind: CueKind.Text, FilePath: null }, "Plain text title was rejected");
            var program = dialog.Result!;
            Check(program.Cue!.Id == original.Cue!.Id, "Editing replaced the title cue ID");
            Check((bool)Call(window, "SaveSceneTile", program)!, "Text title was not persisted");
            Check(store.Load().SceneTiles.Single(scene => scene.Id == program.Id).Cue!.ProgramTitle!.ThemeText == "Tema del sábado", "Title text was not reloaded");
            Check(Scenes().Single(scene => scene.Id == program.Id).Available, "Phone cannot use text title");
            var result = (CommandResult)Call(window, "ExecuteRemote", new RemoteCommand("scene", Guid.NewGuid().ToString("N"), ButtonId: program.Id))!;
            Check(result.Success && session.Live!.Body == "Tema del sábado", "Text title did not reach output");
            Check(session.MoveProgramTitle(1) && session.Live!.Body == "Servir a nuestra comunidad", "Purpose text did not advance");
            Call(window, "CloseOutput");
            Call(window, "RemoveSceneTile", program.Id);
        });
        Test("Program title scene edits, persists and advances from theme to purpose", () =>
        {
            var themeImage = Path.Combine(store.DirectoryPath, "tema.png");
            var purposeImage = Path.Combine(store.DirectoryPath, "proposito.png");
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
                new byte[] { 0, 100, 255, 255, 0, 100, 255, 255, 0, 100, 255, 255, 0, 100, 255, 255 }, 8)));
            using (var stream = File.Create(themeImage)) png.Save(stream);
            File.Copy(themeImage, purposeImage);
            var dialog = new SceneTileDialog(null, []) { Owner = window };
            Exception? callbackError = null;
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    Field<ComboBox>(dialog, "_action").SelectedValue = "program";
                    Call(dialog, "Save");
                    Check(dialog.IsVisible && Field<TextBlock>(dialog, "_error").Text.Contains("tema"), "Empty title was accepted");
                    Field<TextBox>(dialog, "_programTheme").Text = themeImage;
                    Field<TextBox>(dialog, "_programPurpose").Text = purposeImage;
                    dialog.UpdateLayout();
                    CaptureDesignWindow(dialog, Path.Combine(Environment.CurrentDirectory, ".tools", "program-title-editor.png"));
                    Call(dialog, "Save");
                }
                catch (Exception error) { callbackError = error; }
                finally { if (dialog.IsVisible) dialog.DialogResult = false; }
            }));
            var saved = dialog.ShowDialog();
            if (callbackError is not null) throw callbackError;
            Check(saved == true && dialog.Result?.Cue?.ProgramTitle is not null, "Program scene was not saved");
            var program = dialog.Result!;
            Check((bool)Call(window, "SaveSceneTile", program)!, "Program scene was not persisted");
            Check(store.Load().SceneTiles.Single(scene => scene.Id == program.Id).Cue!.ProgramTitle!.PurposeImagePath == purposeImage, "Purpose did not survive reload");
            Check(Scenes().Single(scene => scene.Id == program.Id) is { Available: true, Kind: "image" }, "Phone cannot use title scene");
            var reopened = new SceneTileDialog(program, []);
            Check(Field<TextBox>(reopened, "_programTheme").Text == themeImage
                && Field<TextBox>(reopened, "_programPurpose").Text == purposeImage, "Editing lost the title fields");
            reopened.Close();
            CommandResult Command(string action) => (CommandResult)Call(window, "ExecuteRemote",
                new RemoteCommand(action, Guid.NewGuid().ToString("N"), ButtonId: program.Id))!;
            Check(Command("scene").Success && session.Live!.FilePath == themeImage && session.Live.Kind == CueKind.Image, "Scene did not open on theme image");
            Check(((FrameworkElement)window.FindName("ProgramTitleControls")).Visibility == Visibility.Visible, "Title navigation is hidden");
            Check(Command("next").Success && session.Live!.FilePath == purposeImage && session.Live.Id == program.Cue!.Id,
                "Next did not show purpose in the same scene");
            Check(session.PreviewFollowsLive, "Purpose disconnected the live preview");
            Check(!Command("next").Success && session.Live!.ProgramTitlePage == 1, "End of title skipped into the order");
            Check(Command("previous").Success && session.Live!.ProgramTitlePage == 0, "Previous did not return to theme");
            Check(Command("next").Success && Command("scene").Success && session.Live!.ProgramTitlePage == 0, "Reopening scene did not reset to theme");
            var updatedTheme = Path.Combine(store.DirectoryPath, "tema-actualizado.png");
            File.Copy(themeImage, updatedTheme);
            var editDialog = new SceneTileDialog(program, []) { Owner = window };
            callbackError = null;
            editDialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try { Field<TextBox>(editDialog, "_programTheme").Text = updatedTheme; Call(editDialog, "Save"); }
                catch (Exception error) { callbackError = error; }
                finally { if (editDialog.IsVisible) editDialog.DialogResult = false; }
            }));
            Check(editDialog.ShowDialog() == true, "Updated title image was not accepted");
            if (callbackError is not null) throw callbackError;
            Check((bool)Call(window, "SaveSceneTile", editDialog.Result!)!, "Updated title image was not saved");
            Check(session.Live!.FilePath == updatedTheme, "Editing the active title did not update its projected image");
            Check(Command("scene").Success && session.Live!.FilePath == updatedTheme, "Scene retained the old title image after editing");
            File.Delete(purposeImage);
            Check(!Scenes().Single(scene => scene.Id == program.Id).Available, "A missing purpose image remains available");
            Check(!Command("next").Success && session.Live!.ProgramTitlePage == 0, "Missing image replaced the current output");
            Call(window, "CloseOutput");
            Call(window, "RemoveSceneTile", program.Id);
        });
        Test("Scene editor offers PowerPoint and persists a selected presentation", () =>
        {
            var file = Path.Combine(store.DirectoryPath, "scene-presentation.pptx");
            File.WriteAllBytes(file, []);
            var presentation = new Cue { Kind = CueKind.PowerPoint, Title = "Presentación del culto", FilePath = file };
            var dialog = new SceneTileDialog(null, new[] { cue, presentation }) { Owner = window };
            Exception? callbackError = null;
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    var action = Field<ComboBox>(dialog, "_action");
                    Check(action.Items.OfType<ComboBoxItem>().Any(item => item.Tag as string == "powerpoint"), "PowerPoint is absent from scene actions");
                    action.SelectedValue = "powerpoint";
                    action.Focus(); dialog.UpdateLayout();
                    Check(((Border)action.Template.FindName("ComboFrame", action)).Background is SolidColorBrush focused
                        && focused.Color == Color.FromRgb(48, 48, 48), "Focused scene action uses a light background");
                    Call(dialog, "Save");
                    Check(dialog.IsVisible && !string.IsNullOrEmpty(Field<TextBlock>(dialog, "_error").Text), "PowerPoint scene accepted an empty presentation");
                    var content = Field<ComboBox>(dialog, "_content");
                    Check(content.Items.OfType<ComboBoxItem>().Where(item => item.Tag is Cue)
                        .All(item => ((Cue)item.Tag).Kind == CueKind.PowerPoint), "PowerPoint selector includes unrelated content");
                    content.SelectedItem = content.Items.OfType<ComboBoxItem>().Single(item => (item.Tag as Cue)?.Id == presentation.Id);
                    Check(Field<TextBlock>(dialog, "_error").Text == "", "Choosing a presentation retained its validation error");
                    Field<TextBox>(dialog, "_label").Text = "Presentación";
                    dialog.Width = 800; dialog.Height = 620; dialog.UpdateLayout();
                    foreach (var name in new[] { "SaveScene", "CancelScene" })
                        CheckWithin(Descendants(dialog).OfType<Button>().Single(button => button.Name == name),
                            dialog, new Rect(dialog.RenderSize), name);
                    CaptureDesignWindow(dialog, Path.Combine(Environment.CurrentDirectory, ".tools", "scene-powerpoint-compact.png"));
                    Call(dialog, "Save");
                }
                catch (Exception error) { callbackError = error; }
                finally { if (dialog.IsVisible) dialog.DialogResult = false; }
            }));
            var saved = dialog.ShowDialog();
            if (callbackError is not null) throw callbackError;
            Check(saved == true && dialog.Result is { Action: "powerpoint", Cue.Kind: CueKind.PowerPoint }, "Presentation scene was not saved");
            Check((bool)Call(window, "SaveSceneTile", dialog.Result!)!, "Presentation tile was not persisted");
            var restored = store.Load().SceneTiles.SingleOrDefault(scene => scene.Id == dialog.Result!.Id);
            Check(restored?.Cue?.FilePath == file && restored.Action == "powerpoint", "Reload discarded the presentation scene");
            Check(Scenes().Single(scene => scene.Id == restored!.Id).Available, "Presentation scene is unavailable on remote controls");
            Call(window, "RemoveSceneTile", dialog.Result!.Id);
            File.Delete(file);
        });
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
        Test("A scene can be saved without a visible name and keeps an accessible description", () =>
        {
            var dialog = new SceneTileDialog(custom with { Label = "" }, new[] { cue }) { Owner = window };
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                Call(dialog, "Save");
                if (dialog.IsVisible) dialog.DialogResult = false;
            }));
            Check(dialog.ShowDialog() == true && dialog.Result?.Label == "", "Editor requires a name");
            Check((bool)Call(window, "SaveSceneTile", dialog.Result!)!, "Unnamed scene was not persisted");
            Check(store.Load().SceneTiles.Single(s => s.Id == custom.Id).Label == "", "Reload filled the optional name");
            var button = panel.Children.OfType<Button>().Single(b => b.Tag as string == custom.Id);
            Check(!string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(button)), "Unnamed button has no accessible description");
            Check(Scenes().Single(s => s.Id == custom.Id).Label == "", "Phone renamed the unnamed scene");
        });
        Test("Built-in scenes can be deleted and empty deck stays editable", () =>
        {
            Call(window, "SaveSceneTiles", initial.Select(scene => scene with { Label = "" }).ToList());
            window.UpdateLayout();
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "deckboard", "icon-only-scenes.png"));
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
            Check(first.SceneTiles.Select(s => s.Id).SequenceEqual(new[] { "logo", "blackout", "image", "video", "youtube", "browser", "powerpoint" }), "Default scene order changed");
            first.SceneTiles.RemoveAt(0);
            Check(second.SceneTiles.Count == 7, "Scene lists are shared between workspaces");
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
        Test("PowerPoint scene upgrade preserves existing scenes and permits later removal", () =>
        {
            var store = new WorkspaceStore();
            var custom = new SceneTile { Id = "kept-scene", Label = "Conservar", Action = "image", Color = "#123456" };
            var data = new WorkspaceData { SceneTiles = [custom] }; store.Save(data);
            var path = Path.Combine(store.DirectoryPath, "workspace.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            json.AsObject().Remove("SceneTilesVersion"); File.WriteAllText(path, json.ToJsonString());
            var upgraded = store.Load();
            Check(upgraded.SceneTiles.Count == 2 && upgraded.SceneTiles[0] == custom
                && upgraded.SceneTiles[1] is { Action: "powerpoint", Cue: null }, "Upgrade changed existing scenes");
            upgraded.SceneTiles.RemoveAt(1); store.Save(upgraded);
            Check(store.Load().SceneTiles.Count == 1, "Removed PowerPoint scene reappeared");
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
