using System.Windows;
using System.Windows.Controls;
using Culto.Core;

namespace Culto.Desktop.Dialogs;

public sealed partial class TransitionDialog
{
    private List<TransitionPreset> _presets = [];
    private ComboBox _presetPicker = null!;
    private TextBox _presetName = null!;
    private MoveSettingsEditor _moveIn = null!, _moveOut = null!;
    private Action<TransitionOptions> _loadOptions = null!;
    private bool _loadingPresets;
    private string? _selectedPresetId;
    public IReadOnlyList<TransitionPreset> Presets => _presets.Select(p => p with { Options = p.Options.Normalize() }).ToArray();
    public string? ActivePresetId => _selectedPresetId;

    private void UpdateMoveDraft() => _draft.Move = new MoveOptions { In = _moveIn.Options, Out = _moveOut.Options };

    private void BuildPresetControls(Panel parent, IEnumerable<TransitionPreset>? presets, string? active)
    {
        _presets = (presets ?? []).Select(p => p with { Options = p.Options.Normalize() }).ToList();
        _selectedPresetId = _presets.Any(p => p.Id == active) ? active : null;
        _presetPicker = new ComboBox { Name = "TransitionPresets", SelectedValuePath = "Tag" };
        DialogUi.Label(parent, "Transiciones guardadas", _presetPicker); parent.Children.Add(_presetPicker);
        _presetName = new TextBox { Name = "TransitionPresetName", MaxLength = 80, Text = "Nueva transición" };
        DialogUi.Label(parent, "Nombre", _presetName); parent.Children.Add(_presetName);
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) }; parent.Children.Add(actions);
        var add = DialogUi.Button("+ Añadir transición", AddPreset); add.Name = "AddTransitionPreset"; actions.Children.Add(add);
        var duplicate = DialogUi.Button("Duplicar", () => { CommitPreset(); AddPreset(); }); duplicate.Margin = new Thickness(8, 12, 0, 0); actions.Children.Add(duplicate);
        var remove = DialogUi.Button("Eliminar", RemovePreset); remove.Name = "RemoveTransitionPreset";
        remove.Margin = new Thickness(8, 12, 0, 0); actions.Children.Add(remove);
        RefreshPresetPicker();
    }
    private void RefreshPresetPicker()
    {
        _loadingPresets = true;
        try
        {
            _presetPicker.Items.Clear(); _presetPicker.Items.Add(new ComboBoxItem { Content = "Ajustes actuales", Tag = "" });
            foreach (var preset in _presets) _presetPicker.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset.Id });
            _presetPicker.SelectedValue = _selectedPresetId ?? "";
            _presetName.Text = _presets.FirstOrDefault(p => p.Id == _selectedPresetId)?.Name ?? "Nueva transición";
        }
        finally { _loadingPresets = false; }
    }
    private void CommitPreset()
    {
        var index = _presets.FindIndex(p => p.Id == _selectedPresetId);
        if (index < 0) return;
        _presets[index] = _presets[index] with { Name = string.IsNullOrWhiteSpace(_presetName.Text) ? _presets[index].Name : _presetName.Text.Trim(), Options = Options };
    }
    private void SelectPreset()
    {
        if (_loadingPresets) return;
        CommitPreset();
        _selectedPresetId = _presetPicker.SelectedValue as string;
        if (_selectedPresetId == "") _selectedPresetId = null;
        var selected = _presets.FirstOrDefault(p => p.Id == _selectedPresetId);
        _presetName.Text = selected?.Name ?? "Nueva transición";
        if (selected is not null) _loadOptions(selected.Options.Normalize());
    }
    private void AddPreset()
    {
        var name = _presetName.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = "Nueva transición";
        if (_presets.Any(p => p.Name == name)) name += " (copia)";
        var preset = new TransitionPreset { Name = name, Options = Options };
        _presets.Add(preset); _selectedPresetId = preset.Id; RefreshPresetPicker();
    }
    private void RemovePreset()
    {
        _presets.RemoveAll(p => p.Id == _selectedPresetId);
        _selectedPresetId = null; RefreshPresetPicker();
    }
}
