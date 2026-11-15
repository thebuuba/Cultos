using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Culto.Core;
using static Culto.Desktop.Dialogs.TransitionEditorUi;

namespace Culto.Desktop.Dialogs;

public sealed partial class TransitionDialog
{
    private List<TransitionPreset> _presets = [];
    private ComboBox _presetPicker = null!;
    private ListBox _presetList = null!;
    private TextBox _presetName = null!;
    private MoveSettingsEditor _moveIn = null!, _moveOut = null!;
    private Action<TransitionOptions> _loadOptions = null!;
    private TransitionOptions _currentOptions = new();
    private bool _loadingPresets;
    private string? _selectedPresetId;
    public IReadOnlyList<TransitionPreset> Presets => _presets.Select(p => p with { Options = p.Options.Normalize() }).ToArray();
    public string? ActivePresetId => _selectedPresetId;
    private void UpdateMoveDraft() => _draft.Move = new MoveOptions { In = _moveIn.Options, Out = _moveOut.Options };

    private void BuildPresetControls(Panel parent, IEnumerable<TransitionPreset>? presets, string? active)
    {
        _presets = (presets ?? []).Select(p => p with { Options = p.Options.Normalize() }).ToList();
        _selectedPresetId = _presets.Any(p => p.Id == active) ? active : null;
        parent.Children.Add(Text("Transiciones", 21, bold: true));
        var caption = Text("Guardadas en este equipo", 12, Muted); caption.Margin = new Thickness(0, 5, 0, 20); parent.Children.Add(caption);
        var add = Button("+ Añadir transición", "AddTransitionPreset", AddPreset, true); add.Margin = new Thickness(0, 0, 0, 18); parent.Children.Add(add);
        _presetPicker = new ComboBox { Name = "TransitionPresets", SelectedValuePath = "Tag", Visibility = Visibility.Collapsed }; parent.Children.Add(_presetPicker);
        _presetName = new TextBox { Name = "TransitionPresetName", MaxLength = 80, Text = "Nueva transición" };
        System.Windows.Automation.AutomationProperties.SetName(_presetName, "Nombre de la transición");
        _presetList = new ListBox { Name = "TransitionLibrary", SelectedValuePath = "Tag", Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
        _presetList.SelectionChanged += (_, _) => { if (!_loadingPresets) _presetPicker.SelectedValue = _presetList.SelectedValue; };
        parent.Children.Add(_presetList); RefreshPresetPicker();
        var note = Text("Selecciona un perfil para ajustar su efecto y sus tiempos. Guardar aplica todos los cambios.", 12, Muted); note.Margin = new Thickness(0, 20, 0, 0); parent.Children.Add(note);
    }
    private void RefreshPresetPicker()
    {
        _loadingPresets = true;
        try
        {
            _presetPicker.Items.Clear(); _presetList.Items.Clear();
            AddChoice("Ajustes actuales", "", "Transición del sistema");
            foreach (var preset in _presets) AddChoice(preset.Name, preset.Id, $"{preset.Options.Kind} · {preset.Options.DurationMilliseconds / 1000d:0.00} s");
            _presetPicker.SelectedValue = _selectedPresetId ?? ""; _presetList.SelectedValue = _selectedPresetId ?? "";
            _presetName.Text = _presets.FirstOrDefault(p => p.Id == _selectedPresetId)?.Name ?? "Nueva transición";
        }
        finally { _loadingPresets = false; }
        void AddChoice(string title, string id, string detail)
        {
            _presetPicker.Items.Add(new ComboBoxItem { Content = title, Tag = id });
            var content = new StackPanel { Margin = new Thickness(3, 5, 3, 5) };
            var label = Text(title, 13, bold: true); label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
            content.Children.Add(label); content.Children.Add(Text(detail, 11, Muted));
            var item = new ListBoxItem { Content = content, Tag = id, ToolTip = title, Margin = new Thickness(0, 0, 0, 6), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            item.SetResourceReference(StyleProperty, "EditorPresetItem"); _presetList.Items.Add(item);
        }
    }
    private void CommitPreset()
    {
        var index = _presets.FindIndex(p => p.Id == _selectedPresetId);
        if (index < 0) { _currentOptions = Options; return; }
        _presets[index] = _presets[index] with { Name = string.IsNullOrWhiteSpace(_presetName.Text) ? _presets[index].Name : _presetName.Text.Trim(), Options = Options };
    }
    private void SelectPreset()
    {
        if (_loadingPresets) return;
        CommitPreset(); _selectedPresetId = _presetPicker.SelectedValue as string;
        if (_selectedPresetId == "") _selectedPresetId = null;
        var selected = _presets.FirstOrDefault(p => p.Id == _selectedPresetId);
        _presetName.Text = selected?.Name ?? "Nueva transición";
        _loadOptions((selected?.Options ?? _currentOptions).Normalize());
        RefreshPresetPicker();
    }
    private void AddPreset()
    {
        var name = _presetName.Text.Trim(); if (string.IsNullOrEmpty(name)) name = "Nueva transición";
        var baseName = name; var suffix = 1;
        while (_presets.Any(p => p.Name == name)) { name = baseName + (suffix == 1 ? " (copia)" : $" (copia {suffix})"); suffix++; }
        var preset = new TransitionPreset { Name = name, Options = Options };
        _presets.Add(preset); _selectedPresetId = preset.Id; RefreshPresetPicker(); RefreshVisuals();
    }
    private void RemovePreset()
    {
        _presets.RemoveAll(p => p.Id == _selectedPresetId);
        _selectedPresetId = null; _currentOptions = Options; RefreshPresetPicker(); RefreshVisuals();
    }
}
