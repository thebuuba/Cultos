using System.Windows;
using System.Windows.Controls;
using Culto.Desktop.Dialogs;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private TransitionOptions? _sceneTransitionOverride;
    private TransitionOptions CurrentTransitions => (_sceneTransitionOverride ?? _workspace.Transitions).Normalize();
    private void WithSceneTransition(SceneTile scene, Action action)
    {
        var previous = _sceneTransitionOverride;
        _sceneTransitionOverride = _workspace.TransitionPresets.FirstOrDefault(p => p.Id == scene.TransitionPresetId)?.Options.Normalize() ?? previous;
        try { action(); }
        finally { _sceneTransitionOverride = previous; }
    }
    private string? _transitionPresetSignature;
    private void RefreshTransitionPresetChoices()
    {
        var signature = System.Text.Json.JsonSerializer.Serialize(_workspace.TransitionPresets.Select(p => new { p.Id, p.Name }));
        if (signature == _transitionPresetSignature) return;
        _transitionPresetSignature = signature;
        foreach (var item in SceneTransitionPicker.Items.OfType<ComboBoxItem>().Where(i => (i.Tag as string)?.StartsWith("preset:") == true).ToArray())
            SceneTransitionPicker.Items.Remove(item);
        foreach (var preset in _workspace.TransitionPresets)
            SceneTransitionPicker.Items.Add(new ComboBoxItem { Tag = "preset:" + preset.Id, Content = preset.Name });
    }

    private void TransitionSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new TransitionDialog(_workspace.Transitions, _workspace.TransitionPresets, _workspace.ActiveTransitionPresetId) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var previous = _workspace.Transitions;
        var previousPresets = _workspace.TransitionPresets;
        var previousActive = _workspace.ActiveTransitionPresetId;
        _workspace.Transitions = dialog.Options;
        _workspace.TransitionPresets = dialog.Presets.ToList();
        _workspace.ActiveTransitionPresetId = dialog.ActivePresetId;
        if (!SaveWorkspace()) { _workspace.Transitions = previous; _workspace.TransitionPresets = previousPresets; _workspace.ActiveTransitionPresetId = previousActive; return; }
        if (_output is not null) _output.Transitions = _workspace.Transitions;
        RefreshSceneChoices();
        SetStatus("Transición del sistema guardada.");
    }
}
