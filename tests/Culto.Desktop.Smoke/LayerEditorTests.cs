using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;
using Culto.Remote;

internal static partial class Program
{
    private static void RunLayerEditorTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var initial = workspace.SceneTiles.ToList();
        var store = Field<WorkspaceStore>(window, "_store");
        var session = Field<PresentationSession>(window, "_session");
        var windowsBefore = Application.Current.Windows.Count;
        var previews = new List<ProjectionSurface>();
        var pools = new List<LayerSourcePool>();
        var first = new Cue { Kind = CueKind.Text, Title = "Fondo del editor", Body = "Bienvenidos" };
        var second = new Cue { Kind = CueKind.Bible, Title = "Versículo del editor", Body = "Dios es amor", Reference = "1 Juan 4:8" };
        Cue? savedScene = null;
        SceneTile? tile = null;
        FrameworkElement Preview(SceneComposition composition)
        {
            var preview = new ProjectionSurface(smoothTransitions: true) { Volume = 0 };
            preview.ShowCue(new Cue { Kind = CueKind.Scene, Scene = composition.Copy(), Title = "Vista previa de capas" });
            previews.Add(preview);
            pools.Add(Field<LayerSourcePool>(Field<object>(preview, "_mixer"), "_layerPool"));
            return preview;
        }
        void Click(Window dialog, string caption)
        {
            dialog.UpdateLayout();
            var button = Descendants(dialog).OfType<Button>().Single(b => b.Content as string == caption);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        void Modal(Window dialog, Action action)
        {
            Exception? callbackError = null;
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try { action(); Check(!dialog.IsVisible, "Dialog remained open after Save: " + Field<TextBlock>(dialog, "_error").Text); }
                catch (Exception error) { callbackError = error; dialog.Close(); }
            }));
            var accepted = dialog.ShowDialog();
            if (callbackError is not null) throw callbackError;
            Check(accepted == true, "Layer dialog did not save");
        }
        try
        {
            Test("Layer animation controls persist atomically and replay keeps the live preview", () =>
            {
                var dialog = new SceneLayersDialog(null, [first], Preview) { Owner = window };
                Modal(dialog, () =>
                {
                    Click(dialog, "Añadir seleccionada");
                    Field<TextBox>(dialog, "_body").Text = "Texto personalizado de la plantilla";
                    Field<ComboBox>(dialog, "_entrance").SelectedValue = "Rise";
                    Field<ComboBox>(dialog, "_exit").SelectedValue = "Fade";
                    var timing = Field<Dictionary<string, TextBox>>(dialog, "_timing");
                    timing["Duración (ms)"].Text = "850";
                    timing["Retardo (ms)"].Text = "250";
                    timing["Permanencia (ms, 0 = continua)"].Text = "2400";
                    var loop = Field<CheckBox>(dialog, "_loop"); loop.IsChecked = true;
                    loop.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    var layer = Field<SceneComposition>(dialog, "_scene").Layers.Single();
                    Check(layer.Animation.Entrance == "Rise" && layer.Animation.Exit == "Fade" && layer.Loop,
                        "Animation selections or loop were not applied");
                    Field<TextBox>(dialog, "_name").Text = "No aplicar todavía";
                    timing["Duración (ms)"].Text = "-1";
                    Click(dialog, "Repetir vista previa");
                    Check(Field<TextBlock>(dialog, "_error").Text.Length > 0 && layer.Name == first.Title && layer.Animation.DurationMilliseconds == 850,
                        "Invalid animation partially changed the working layer");
                    timing["Duración (ms)"].Text = "850";
                    // Commit pending fields, then replay the same renderer/source pool.
                    loop.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    var preview = Field<Border>(dialog, "_preview").Child;
                    var pool = pools.Last(); var entries = pool.Entries.ToArray();
                    Field<TextBox>(dialog, "_name").RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
                    Check(ReferenceEquals(preview, Field<Border>(dialog, "_preview").Child), "Unchanged field focus loss replaced the renderer");
                    Click(dialog, "Repetir vista previa");
                    Check(ReferenceEquals(preview, Field<Border>(dialog, "_preview").Child) && pool.Entries.SequenceEqual(entries),
                        "Replay replaced the live renderer or its sources");
                    Click(dialog, "Guardar capas");
                });
                var saved = dialog.SelectedCue!.Scene!.Layers.Single();
                Check(saved.Source.Body == "Texto personalizado de la plantilla", "Layer text content was not saved");
                Check(saved.Animation is { Entrance: "Rise", Exit: "Fade", DurationMilliseconds: 850, DelayMilliseconds: 250, HoldMilliseconds: 2400 } && saved.Loop,
                    "Saved animation timing or loop was lost");
            });
            Test("Editing shared text detaches only the changed layer source", () =>
            {
                var dialog = new SceneLayersDialog(null, [first], Preview) { Owner = window };
                Modal(dialog, () =>
                {
                    Click(dialog, "Añadir seleccionada"); Click(dialog, "Añadir seleccionada");
                    var scene = Field<SceneComposition>(dialog, "_scene");
                    var originalId = scene.Layers[0].SourceId;
                    Check(scene.Layers[1].SourceId == originalId, "Repeated text source did not begin shared");
                    Field<TextBox>(dialog, "_body").Text = "Texto independiente";
                    var loop = Field<CheckBox>(dialog, "_loop");
                    loop.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    var editedId = scene.Layers[1].SourceId;
                    Check(editedId != originalId && scene.Layers[0].Source.Body == "Bienvenidos" && scene.Layers[1].Source.Body == "Texto independiente",
                        "Editing shared text overwrote or retained the shared source");
                    loop.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    Check(scene.Layers[1].SourceId == editedId, "Unchanged text needlessly changed source identity");
                    Click(dialog, "Guardar capas");
                });
            });
            Test("Templates require explicit apply, reuse backgrounds and cancellation isolates edits", () =>
            {
                var background = new Cue { Kind = CueKind.Image, Title = "Fondo elegido" };
                var original = new Cue { Kind = CueKind.Scene, Title = "Original", Scene = new SceneComposition
                {
                    Layers = [new SceneLayer { Source = background, Name = "Fondo", Width = 1920, Height = 1080 }]
                } };
                var dialog = new SceneLayersDialog(original, [first], Preview) { Owner = window };
                Exception? callbackError = null;
                dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    try
                    {
                        var templates = Field<ComboBox>(dialog, "_templates");
                        Check(templates.Items.Count == 4, "Four templates are not available");
                        templates.SelectedValue = "reading";
                        Check(Field<SceneComposition>(dialog, "_scene").Layers.Count == 1, "Selecting a template replaced edits without Apply");
                        Click(dialog, "Aplicar plantilla");
                        var scene = Field<SceneComposition>(dialog, "_scene");
                        Check(scene.Layers.Count > 1 && scene.Layers[0].Source.Id == background.Id,
                            "Template did not reuse the existing background when selected source was text");
                        Check(scene.Layers.Any(layer => layer.Animation.Entrance != "None"), "Template has no animated content");
                        PumpFor(900); dialog.UpdateLayout();
                        CaptureDesignWindow(dialog, Path.Combine(Environment.CurrentDirectory, ".tools", "scene-template-editor.png"));
                        ((ScrollViewer)dialog.Content).ScrollToEnd(); dialog.UpdateLayout();
                        CaptureDesignWindow(dialog, Path.Combine(Environment.CurrentDirectory, ".tools", "scene-template-editor-fields.png"));
                        Click(dialog, "Cancelar");
                    }
                    catch (Exception error) { callbackError = error; dialog.Close(); }
                }));
                Check(dialog.ShowDialog() == false, "Cancel accepted the template");
                if (callbackError is not null) throw callbackError;
                Check(dialog.SelectedCue is null && original.Scene.Layers.Count == 1 && original.Scene.Layers[0].Animation.Entrance == "None",
                    "Cancelled template changed the original scene");
            });
            Test("Every template applies with the selected background and editable text", () =>
            {
                var background = new Cue { Kind = CueKind.Image, Title = "Fondo seleccionado" };
                var dialog = new SceneLayersDialog(null, [background], Preview) { Owner = window };
                Modal(dialog, () =>
                {
                    foreach (var id in new[] { "welcome", "reading", "sermon", "announcement" })
                    {
                        Field<ComboBox>(dialog, "_templates").SelectedValue = id;
                        Click(dialog, "Aplicar plantilla");
                        var scene = Field<SceneComposition>(dialog, "_scene");
                        Check(scene.Layers[0].Source.Id == background.Id && scene.Layers.Count >= 3,
                            "Template failed to apply with selected background: " + id);
                    }
                    Field<TextBox>(dialog, "_body").Text = "Domingo a las 10:00";
                    Click(dialog, "Guardar capas");
                });
                Check(dialog.SelectedCue!.Scene!.Layers.Last().Source.Body == "Domingo a las 10:00", "Template text cannot be personalized");
            });
            Test("Layer editor adds, transforms, reorders, hides, mutes and removes sources with silent live preview", () =>
            {
                var dialog = new SceneLayersDialog(null, [first, second], Preview) { Owner = window };
                Modal(dialog, () =>
                {
                    var sources = Field<ComboBox>(dialog, "_sources");
                    sources.SelectedIndex = 0; Click(dialog, "Añadir seleccionada");
                    sources.SelectedIndex = 1; Click(dialog, "Añadir seleccionada");
                    var list = Field<ListBox>(dialog, "_layers");
                    Check(list.Items.Count == 2, "Sources were not added through UI");
                    Field<TextBox>(dialog, "_name").Text = "Versículo superpuesto";
                    var numbers = Field<Dictionary<string, TextBox>>(dialog, "_numbers");
                    foreach (var (key, value) in new[] { ("X", "120"), ("Y", "80"), ("Ancho", "900"), ("Alto", "500"),
                        ("Rotación (°)", "12"), ("Opacidad (0–1)", "0.65"), ("Volumen (0–100)", "37") }) numbers[key].Text = value;
                    var visible = Field<CheckBox>(dialog, "_visible"); visible.IsChecked = false;
                    visible.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    var muted = Field<CheckBox>(dialog, "_muted"); muted.IsChecked = true;
                    muted.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    Click(dialog, "Hacia atrás ↓");
                    Check(((SceneLayer)list.Items[0]).Name == "Versículo superpuesto", "Reorder or name edit failed");
                    Click(dialog, "Hacia delante ↑");
                    Check(((SceneLayer)list.Items[1]).Source.Id == second.Id, "Forward reorder failed");
                    // Repeated additions share the source identity but retain independent layer IDs.
                    Click(dialog, "Añadir seleccionada");
                    Check(list.Items.Count == 3, "Third source was not added");
                    var layers = list.Items.Cast<SceneLayer>().ToArray();
                    Check(layers[1].SourceId == layers[2].SourceId && layers[1].Id != layers[2].Id, "Shared source identity was lost");
                    Click(dialog, "Eliminar"); Check(list.Items.Count == 2, "Remove layer failed");
                    Check(Field<Border>(dialog, "_preview").Child is ProjectionSurface { Volume: 0 }, "Editor lacks silent real renderer preview");
                    Check(pools.Last().Entries.Any(), "Real preview did not acquire a visible source");
                    Check(pools.Last().Entries.All(entry => entry.Surface.Volume == 0), "Layer preview source can play audible audio");
                    Field<TextBox>(dialog, "_title").Text = "Escena de prueba por capas";
                    Click(dialog, "Guardar capas");
                });
                savedScene = dialog.SelectedCue;
                Check(savedScene?.Scene?.Layers.Count == 2, "Saved layered cue missing");
                var edited = savedScene!.Scene!.Layers[1];
                Check(edited.X == 120 && edited.Y == 80 && edited.Width == 900 && edited.Height == 500 && edited.Rotation == 12,
                    "Transforms were not saved");
                Check(Math.Abs(edited.Opacity - .65) < .001 && edited.Volume == 37 && !edited.Visible && edited.Muted, "Opacity/show/mute/volume lost");
                Check(pools.All(pool => !pool.Entries.Any()), "Closing/replacing preview leaked source pool entries");
                Check(Application.Current.Windows.Count == windowsBefore, "Editor leaked a preview window");
            });
            Test("Scene tile edits layers and saves fixed layered content", () =>
            {
                Check(savedScene is not null, "Previous editor did not produce a scene");
                var dialog = new SceneTileDialog(new SceneTile { Label = "Capas propias", Action = "scene", Cue = savedScene },
                    [first, second, savedScene!], null, Preview) { Owner = window };
                Modal(dialog, () =>
                {
                    Check(Field<ComboBox>(dialog, "_action").SelectedValue as string == "scene", "Scene action not selected");
                    Exception? nestedError = null;
                    dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        var editor = Application.Current.Windows.OfType<SceneLayersDialog>().Single();
                        try
                        {
                            Field<TextBox>(editor, "_title").Text = "Contenido de capas fijo";
                            Click(editor, "Guardar capas");
                            Check(!editor.IsVisible, "Nested layer editor remained open after Save");
                        }
                        catch (Exception error) { nestedError = error; editor.Close(); }
                    }));
                    Click(dialog, "Editar capas…");
                    if (nestedError is not null) throw nestedError;
                    Field<TextBox>(dialog, "_label").Text = "Mi escena de capas";
                    Click(dialog, "Guardar escena");
                });
                tile = dialog.Result;
                Check(tile is { Action: "scene", Cue.Kind: CueKind.Scene } && tile.Cue.Title == "Contenido de capas fijo", "Tile did not pin edited composition");
                Check(tile!.Cue!.Scene!.Layers[0].SourceId == savedScene!.Scene!.Layers[0].SourceId, "Editing layers changed shared identity");
                Check(pools.All(pool => !pool.Entries.Any()), "Nested layer editor leaked source entries");
            });
            Test("Layered tiles persist, duplicate independently and project from the phone scene route", () =>
            {
                Check(tile?.Cue?.Scene is not null, "No layered tile to project");
                Check((bool)Call(window, "SaveSceneTile", tile)!, "Layered tile save failed");
                var restored = store.Load().SceneTiles.Single(s => s.Id == tile!.Id);
                Check(restored.Action == "scene" && restored.Cue?.Scene?.Layers.Count == 2, "Workspace lost scene composition");
                Check(restored.Cue!.Scene!.Layers[1].Muted && !restored.Cue.Scene.Layers[1].Visible, "Workspace lost mute/visibility");
                var scenes = ((IReadOnlyList<RemoteScene>)Call(window, "BuildRemoteScenes")!).ToArray();
                Check(scenes.Single(s => s.Id == tile!.Id).Available, "Phone reports a valid layered scene unavailable");
                Call(window, "DuplicateSceneTile", tile);
                var duplicate = workspace.SceneTiles.Last();
                Check(duplicate.Id != tile!.Id && duplicate.Cue!.Id != tile.Cue!.Id, "Duplicated tile IDs did not change");
                Check(duplicate.Cue!.Scene!.Layers[0].SourceId == tile.Cue!.Scene!.Layers[0].SourceId, "Duplicate broke shared source identity");
                duplicate.Cue.Scene.Layers[0].X = 444;
                duplicate.Cue.Scene.Layers[0].Source.Body = "Otro texto";
                Check(tile.Cue.Scene.Layers[0].X == 0 && tile.Cue.Scene.Layers[0].Source.Body == "Bienvenidos", "Duplicate edits changed original layers");
                Call(window, "PrepareCue", new Cue { Kind = CueKind.Text, Title = "Otro", Body = "No proyectar" }, null);
                var result = (CommandResult)Call(window, "ExecuteRemoteSceneMode", tile.Id, false)!;
                Check(result.Success && session.Live?.Kind == CueKind.Scene && session.Live.Id == tile.Cue.Id, "Remote scene route did not project its fixed composition");
                PumpFor(250);
                Check(((IReadOnlyList<RemoteScene>)Call(window, "BuildRemoteScenes")!).Single(s => s.Id == tile.Id).Active, "Layered scene lacks active marker");
                Call(window, "CloseOutput");
            });
        }
        finally
        {
            Call(window, "CloseOutput"); Call(window, "SaveSceneTiles", initial);
            foreach (var preview in previews) preview.Dispose();
        }
    }
}
