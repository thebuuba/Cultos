using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;
using Culto.Desktop.Projection;
using Culto.Remote;

internal static partial class Program
{
    private static void RunTransitionPresetTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        T Control<T>(TransitionDialog dialog, string name) where T : FrameworkElement => Descendants(dialog).OfType<T>().Single(c => c.Name == name);
        void Press(TransitionDialog dialog, string name) => Control<Button>(dialog, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Editor(Action<TransitionDialog> act)
        {
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var dialog = Application.Current.Windows.OfType<TransitionDialog>().Single();
                try { act(dialog); }
                catch { dialog.DialogResult = false; throw; }
            }));
            Call(window, "TransitionSettingsClick", window, new RoutedEventArgs());
        }
        Test("Custom Move profile keeps independent in/out controls and native values after restart", () =>
        {
            Editor(dialog =>
            {
                Control<ComboBox>(dialog, "TransitionKind").SelectedValue = "Move";
                Control<Slider>(dialog, "TransitionDuration").Value = 950;
                Control<ComboBox>(dialog, "MoveInPosition").SelectedValue = 18;
                Control<Slider>(dialog, "MoveInZoom").Value = 75;
                Control<Slider>(dialog, "MoveInCurve").Value = .6;
                Control<ComboBox>(dialog, "MoveInEasing").SelectedValue = 2;
                Control<ComboBox>(dialog, "MoveInFunction").SelectedValue = 9;
                Control<ComboBox>(dialog, "MoveOutPosition").SelectedValue = 34;
                Control<Slider>(dialog, "MoveOutZoom").Value = 150;
                Control<ComboBox>(dialog, "MoveOutFunction").SelectedValue = 10;
                Control<TextBox>(dialog, "TransitionPresetName").Text = "Entrada desde arriba";
                Press(dialog, "AddTransitionPreset");
                Press(dialog, "SaveTransitions");
            });
            var saved = new WorkspaceStore().Load(); var preset = saved.TransitionPresets.Single();
            Check(preset.Name == "Entrada desde arriba" && saved.ActiveTransitionPresetId == preset.Id, "Profile name/selection was lost");
            Check(preset.Options.Kind == "Move" && preset.Options.DurationMilliseconds == 950, "Profile timing/type was lost");
            Check(preset.Options.Move.In is { Position: 18, Zoom: 75, Curve: .6, Easing: 2, Function: 9 }, "Incoming native settings changed");
            Check(preset.Options.Move.Out is { Position: 34, Zoom: 150, Function: 10 }, "Outgoing native settings changed");
            Check(workspace.Transitions == preset.Options, "Active transition differs from its saved profile");
            var selector = (ComboBox)window.FindName("SceneTransitionPicker");
            Check(selector.Items.OfType<ComboBoxItem>().Any(i => i.Tag as string == "preset:" + preset.Id), "Profile absent from quick selector");
        });
        Test("Preview runs the chosen Move settings without touching live output", () =>
        {
            var session = Field<PresentationSession>(window, "_session"); var before = session.Live;
            Editor(dialog =>
            {
                var preview = Control<Button>(dialog, "PreviewTransition"); preview.BringIntoView(); dialog.UpdateLayout();
                preview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => !Field<bool>(dialog, "_previewPlaying"), "Native preview did not finish");
                Check(Field<ProjectionSurface?>(dialog, "_transitionPreview") is not null, "Preview surface was not created");
                Check(!Descendants(dialog).OfType<TextBlock>().Any(t => t.Text.StartsWith("No se pudo") || t.Text.Contains("no terminó")), "Preview reported a native error");
                var captures = System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "move-editor"); System.IO.Directory.CreateDirectory(captures);
                CaptureDesignWindow(dialog, System.IO.Path.Combine(captures, "preview.png"));
                Control<ComboBox>(dialog, "MoveInPosition").BringIntoView(); dialog.UpdateLayout(); PumpFor(100);
                CaptureDesignWindow(dialog, System.IO.Path.Combine(captures, "settings.png"));
                dialog.DialogResult = false;
            });
            Check(ReferenceEquals(session.Live, before), "Preview projected a sample into the live service");
        });
        Test("Canceling profile edits or deletion preserves the saved profile", () =>
        {
            var original = workspace.TransitionPresets.Single();
            Editor(dialog =>
            {
                Control<Slider>(dialog, "MoveInZoom").Value = 25;
                Press(dialog, "RemoveTransitionPreset");
                dialog.DialogResult = false;
            });
            Check(workspace.TransitionPresets.Single() == original && new WorkspaceStore().Load().TransitionPresets.Single() == original,
                "Cancel changed a stored profile");
        });
        Test("Several named transitions can be selected and deleted without changing other profiles", () =>
        {
            var first = workspace.TransitionPresets.Single();
            Editor(dialog =>
            {
                Control<TextBox>(dialog, "TransitionPresetName").Text = "Segundo";
                Press(dialog, "AddTransitionPreset");
                Control<Slider>(dialog, "MoveInZoom").Value = 125;
                Press(dialog, "SaveTransitions");
            });
            var selector = (ComboBox)window.FindName("SceneTransitionPicker");
            selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().Single(i => i.Tag as string == "preset:" + first.Id);
            Check(workspace.TransitionPresets.Single(p => p.Id == first.Id).Name == first.Name, "Adding a new profile renamed its source");
            Check(workspace.Transitions.Move.In.Zoom == 75, "Selecting first profile used the second profile options");
            Editor(dialog => { Press(dialog, "RemoveTransitionPreset"); Press(dialog, "SaveTransitions"); });
            Check(workspace.TransitionPresets.Count == 1 && workspace.ActiveTransitionPresetId is null, "Deleted active profile remains selected");
            Check(!selector.Items.OfType<ComboBoxItem>().Any(i => i.Tag as string == "preset:" + first.Id), "Deleted profile remains selectable");
        });
        Test("Scene transition override applies from phone and desktop without replacing system settings", () =>
        {
            var preset = new TransitionPreset { Name = "Escena con zoom", Options = new TransitionOptions { Kind = "Zoom", DurationMilliseconds = 100,
                AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 } };
            workspace.TransitionPresets.Add(preset);
            var global = new TransitionOptions { Kind = "Cut" }; workspace.Transitions = global;
            var scene = new SceneTile { Id = "own-transition", Label = "Escena", Action = "cue", TransitionPresetId = preset.Id,
                Cue = new Cue { Kind = CueKind.Text, Body = "Perfil de escena" } };
            Check((bool)Call(window, "SaveSceneTile", scene)!, "Scene with preset was not saved");
            Check(new WorkspaceStore().Load().SceneTiles.Single(s => s.Id == scene.Id).TransitionPresetId == preset.Id, "Scene preset was not persisted");
            var result = (CommandResult)Call(window, "ExecuteRemoteSceneMode", scene.Id, false)!;
            var output = Field<ProjectionSurface>(window, "_output");
            Check(result.Success && output.Transitions.Kind == "Zoom", "Phone scene ignored its own preset");
            Check(workspace.Transitions == global, "Scene replaced the system transition");
            Until(() => !output.IsTransitioning, "Scene transition did not finish");
            Call(window, "PrepareCue", new Cue { Body = "Otra escena" }, null); Call(window, "ProjectPrepared");
            Check(output.Transitions.Kind == "Cut", "Scene preset leaked into later normal projections");
            var button = ((Panel)window.FindName("OutputBackdropsPanel")).Children.OfType<Button>().Single(b => b.Tag as string == scene.Id);
            var menu = new ContextMenu { PlacementTarget = button };
            TransitionOptions? menuOptions = null;
            Call(window, "WithSceneMenuTransition", menu, new Action(() => menuOptions = (TransitionOptions)typeof(MainWindow)
                .GetProperty("CurrentTransitions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(window)!));
            Check(menuOptions == preset.Options && workspace.Transitions == global, "Source menu ignored the scene preset or changed the global one");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(output.Transitions.Kind == "Zoom" && workspace.Transitions == global, "Desktop scene ignored its own preset");
            Call(window, "CloseOutput");
        });
        Test("Audio-only scene snapshots its own fade settings instead of previous output settings", () =>
        {
            var preset = new TransitionPreset { Name = "Audio lento", Options = new TransitionOptions { Kind = "Move",
                AudioFadeInMilliseconds = 1500, AudioFadeOutMilliseconds = 800 } };
            workspace.TransitionPresets.Add(preset);
            Call(window, "PrepareCue", new Cue { Kind = CueKind.Text, Body = "Mantener esta imagen" }, null);
            Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            output.Transitions = new TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
            var scene = new SceneTile { Id = "audio-transition-audit", Label = "Audio", Action = "video", TransitionPresetId = preset.Id,
                Cue = new Cue { Kind = CueKind.Video, FilePath = System.IO.Path.GetFullPath("tests/Culto.Renderer.Smoke/fixtures/test.mkv") } };
            Call(window, "SaveSceneTile", scene);
            var result = (CommandResult)Call(window, "ExecuteRemoteSceneMode", scene.Id, true)!;
            Check(result.Success && output.Transitions == preset.Options, "Audio-only projection reused previous scene settings");
            Call(window, "CloseOutput");
        });
    }
}
