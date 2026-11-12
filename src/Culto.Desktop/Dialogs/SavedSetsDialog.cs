using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Culto.Core;
using Microsoft.Win32;

namespace Culto.Desktop.Dialogs;

public sealed class SavedSetsDialog : Window
{
    private readonly ListBox _sets = new() { DisplayMemberPath = "Name", MinHeight = 90 };
    private readonly ListBox _items = new() { DisplayMemberPath = "Title", MinHeight = 90 };
    private readonly TextBlock _count = new();
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly List<Button> _selectionActions = [];
    private readonly Button _append, _replace, _up, _down, _remove;
    private readonly bool _videosOnly;
    public List<SavedSet> Sets { get; }
    public SavedSet? RequestedSet { get; private set; }
    public bool ReplaceOrder { get; private set; }
    private SavedSet? SelectedSet => _sets.SelectedItem as SavedSet;

    public SavedSetsDialog(IEnumerable<SavedSet> sets, bool videosOnly, Cue? selectedVideo = null)
    {
        _videosOnly = videosOnly;
        Sets = sets.Select(set => new SavedSet { Id = set.Id, Name = set.Name, Items = set.CreateItems() }).ToList();
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = videosOnly ? "Colecciones de videos" : "Cultos guardados";
        Width = 760; Height = 660; MinWidth = 540; MinHeight = 560;
        MaxHeight = SystemParameters.WorkArea.Height - 32;
        MaxWidth = SystemParameters.WorkArea.Width - 32;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new Thickness(22) };
        foreach (var height in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto,
            new GridLength(1.5, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto })
            root.RowDefinitions.Add(new RowDefinition { Height = height });
        root.Children.Add(new TextBlock { Text = Title, FontSize = 24, Margin = new Thickness(0, 0, 0, 16) });
        var setCard = DialogUi.Card(_sets); setCard.Padding = new Thickness(8); Grid.SetRow(setCard, 1); root.Children.Add(setCard);
        AutomationProperties.SetName(_sets, Title);
        var setActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        Grid.SetRow(setActions, 2); root.Children.Add(setActions);
        if (videosOnly) AddButton(setActions, "Nueva colección", CreateCollection);
        _selectionActions.Add(AddButton(setActions, "Renombrar", Rename));
        _selectionActions.Add(AddButton(setActions, "Eliminar", Delete));
        _count.Style = (Style)FindResource("MutedText"); _count.VerticalAlignment = VerticalAlignment.Center;
        _count.Margin = new Thickness(8, 0, 0, 0); setActions.Children.Add(_count);
        var itemCard = DialogUi.Card(_items); itemCard.Padding = new Thickness(8); Grid.SetRow(itemCard, 3); root.Children.Add(itemCard);
        AutomationProperties.SetName(_items, "Contenido guardado");
        var itemActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetRow(itemActions, 4); root.Children.Add(itemActions);
        if (videosOnly)
        {
            _selectionActions.Add(AddButton(itemActions, "Añadir videos…", AddVideos));
            if (selectedVideo?.Kind == CueKind.Video)
                _selectionActions.Add(AddButton(itemActions, "Añadir seleccionado", () =>
                {
                    if (SelectedSet is not { } set) return;
                    set.Items.AddRange(SavedSet.Capture("", [selectedVideo]).Items); RefreshItems(set.Items.Count - 1);
                }));
        }
        _up = AddButton(itemActions, "Subir", () => Move(-1));
        _down = AddButton(itemActions, "Bajar", () => Move(1));
        _remove = AddButton(itemActions, "Quitar", Remove);
        _error.Foreground = (Brush)FindResource("AmberBrush");
        _error.Margin = new Thickness(0, 8, 0, 0); Grid.SetRow(_error, 5); root.Children.Add(_error);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        Grid.SetRow(actions, 6); root.Children.Add(actions);
        var cancel = AddButton(actions, "Cancelar", () => DialogResult = false); cancel.IsCancel = true;
        AddButton(actions, "Guardar", () => DialogResult = true);
        _replace = AddButton(actions, "Reemplazar orden", () => UseSet(true));
        _append = AddButton(actions, "Añadir al orden", () => UseSet(false));
        _append.Style = (Style)FindResource("PrimaryButton");
        Content = root;
        _sets.SelectionChanged += (_, _) => RefreshItems();
        _items.SelectionChanged += (_, _) => UpdateActions();
        RefreshSets(Sets.FirstOrDefault());
    }

    private static Button AddButton(Panel panel, string title, Action action)
    {
        var button = DialogUi.Button(title, action);
        button.Margin = new Thickness(0, 0, 6, 6); button.Padding = new Thickness(11, 7, 11, 7);
        AutomationProperties.SetName(button, title); panel.Children.Add(button); return button;
    }
    private void RefreshSets(SavedSet? selected)
    {
        _sets.ItemsSource = null; _sets.ItemsSource = Sets;
        _sets.SelectedItem = selected ?? Sets.FirstOrDefault(); RefreshItems();
    }
    private void RefreshItems(int index = -1)
    {
        _items.ItemsSource = null; _items.ItemsSource = SelectedSet?.Items;
        _items.SelectedIndex = index;
        _count.Text = SelectedSet is { } set ? $"{set.Items.Count} contenidos" : "Sin elementos guardados";
        UpdateActions();
    }
    private void UpdateActions()
    {
        foreach (var button in _selectionActions) button.IsEnabled = SelectedSet is not null;
        _append.IsEnabled = _replace.IsEnabled = SelectedSet?.Items.Count > 0;
        _remove.IsEnabled = _items.SelectedIndex >= 0;
        _up.IsEnabled = _items.SelectedIndex > 0;
        _down.IsEnabled = _items.SelectedIndex >= 0 && _items.SelectedIndex < _items.Items.Count - 1;
    }
    private void CreateCollection()
    {
        var name = PromptDialog.Ask(this, "Nueva colección", "Nombre", "Alabanza");
        if (name is null) return;
        var set = SavedSet.Capture(name, []); Sets.Add(set); RefreshSets(set);
    }
    private void Rename()
    {
        if (SelectedSet is not { } set) return;
        var name = PromptDialog.Ask(this, "Renombrar", "Nombre", set.Name);
        if (name is null) return;
        set.Name = name; RefreshSets(set);
    }
    private void Delete()
    {
        if (SelectedSet is not { } set) return;
        Sets.Remove(set); RefreshSets(null);
    }
    private void AddVideos()
    {
        if (!_videosOnly || SelectedSet is not { } set) return;
        var dialog = new OpenFileDialog { Title = "Añadir videos a la colección", Multiselect = true,
            Filter = "Videos|*.mkv;*.mp4;*.mov;*.webm;*.avi;*.wmv" };
        if (dialog.ShowDialog(this) != true) return;
        var failures = 0;
        foreach (var path in dialog.FileNames)
        {
            try
            {
                var cue = MediaLibrary.FromFile(path);
                if (cue.Kind != CueKind.Video) { failures++; continue; }
                set.Items.Add(cue);
            }
            catch (Exception) { failures++; }
        }
        _error.Text = failures > 0 ? $"{failures} archivos no se pudieron añadir." : "";
        RefreshItems(set.Items.Count - 1);
    }
    private void Move(int delta)
    {
        if (SelectedSet is not { } set) return;
        var index = _items.SelectedIndex; var target = index + delta;
        if (index < 0 || target < 0 || target >= set.Items.Count) return;
        var item = set.Items[index]; set.Items.RemoveAt(index); set.Items.Insert(target, item); RefreshItems(target);
    }
    private void Remove()
    {
        if (SelectedSet is not { } set || _items.SelectedIndex < 0) return;
        var index = _items.SelectedIndex; set.Items.RemoveAt(index); RefreshItems(Math.Min(index, set.Items.Count - 1));
    }
    private void UseSet(bool replace)
    {
        if (SelectedSet is not { Items.Count: > 0 } set) return;
        RequestedSet = set; ReplaceOrder = replace; DialogResult = true;
    }
}
