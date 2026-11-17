using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Culto.Core;
using static Culto.Desktop.Dialogs.TransitionEditorUi;

namespace Culto.Desktop.Dialogs;

public sealed partial class BibleStyleDialog
{
    private void CreateTransitionSelector(StackPanel controls)
    {
        var choices = new[] { (Id: "System", Name: "Del sistema", Family: ""), (Id: "Cut", Name: "Corte", Family: ""), (Id: "Dissolve", Name: "Disolver", Family: "") }
            .Concat(BibleTextTransitionCatalog.All.Select(preset => (preset.Id, preset.Name, preset.Family))).ToArray();
        var favorites = new HashSet<string>(_draft.FavoriteTransitions.Split('|', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        var updating = false;
        var filters = new StackPanel();
        controls.Children.Add(new Expander { Name = "BibleTransitionFilters", Header = "Buscar y filtrar efectos", Content = filters, IsExpanded = false,
            Margin = new Thickness(0, 0, 0, 14) });
        var search = new TextBox { Name = "BibleTransitionSearch", ToolTip = "Busca por nombre o familia, con o sin tildes." };
        AddField(filters, "Buscar efecto", search);
        var family = new ComboBox { Name = "BibleTransitionFamily", SelectedValuePath = "Tag", MaxDropDownHeight = 260 };
        family.Items.Add(new ComboBoxItem { Content = "Todas las familias", Tag = "" });
        foreach (var name in BibleTextTransitionCatalog.All.Select(preset => preset.Family).Distinct())
            family.Items.Add(new ComboBoxItem { Content = name, Tag = name });
        family.SelectedIndex = 0;
        AddField(filters, "Familia", family);
        var favoritesOnly = new CheckBox
        {
            Name = "BibleTransitionFavoritesOnly", Content = "Solo favoritos", VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(favoritesOnly, "Mostrar solo efectos favoritos");
        var matches = Text("", 12, Muted);
        matches.Name = "BibleTransitionMatches";
        matches.Margin = new Thickness(8, 0, 0, 0);
        AutomationProperties.SetLiveSetting(matches, AutomationLiveSetting.Polite);
        var filterRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(matches, Dock.Right); filterRow.Children.Add(matches); filterRow.Children.Add(favoritesOnly);
        filters.Children.Add(filterRow);
        var picker = new ComboBox { Name = "BibleTransition", SelectedValuePath = "Tag", MaxDropDownHeight = 290, MinWidth = 0 };
        var pickerLabel = new FrameworkElementFactory(typeof(TextBlock));
        pickerLabel.SetBinding(TextBlock.TextProperty, new Binding());
        pickerLabel.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        pickerLabel.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
        picker.ItemTemplate = new DataTemplate { VisualTree = pickerLabel };
        AutomationProperties.SetName(picker, "Entrada del versículo");
        var favorite = new Button
        {
            Name = "BibleTransitionFavorite", Content = "☆", Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(6, 0, 0, 0), FontSize = 19,
            Style = (System.Windows.Style)FindResource("EditorButton")
        };
        var selectionRow = new DockPanel();
        DockPanel.SetDock(favorite, Dock.Right); selectionRow.Children.Add(favorite); selectionRow.Children.Add(picker);
        AddField(controls, "Texto bíblico", selectionRow);
        var selected = Text("", 12);
        selected.Name = "BibleTransitionSelected";
        selected.Margin = new Thickness(0, 0, 0, 12);
        controls.Children.Add(selected);

        var referencePicker = new ComboBox { Name = "BibleReferenceTransition", SelectedValuePath = "Tag", MaxDropDownHeight = 290, MinWidth = 0,
            ItemTemplate = picker.ItemTemplate };
        AddField(controls, "Referencia", referencePicker);
        var referenceSelected = Text("", 12, Muted);
        referenceSelected.Name = "BibleReferenceTransitionSelected";
        referenceSelected.Margin = new Thickness(0, 0, 0, 12);
        controls.Children.Add(referenceSelected);

        void UpdateSelection()
        {
            var name = choices.FirstOrDefault(choice => choice.Id == _draft.Transition).Name ?? _draft.Transition;
            selected.Text = "Seleccionado: " + name;
            var referenceName = _draft.ReferenceTransition is null ? "Usar la misma del texto"
                : choices.FirstOrDefault(choice => choice.Id == _draft.ReferenceTransition).Name ?? _draft.ReferenceTransition;
            referenceSelected.Text = "Seleccionado: " + referenceName;
            referenceSelected.Visibility = _draft.ReferenceTransition is null ? Visibility.Collapsed : Visibility.Visible;
            referencePicker.ToolTip = referenceName;
            picker.ToolTip = name;
            var isFavorite = favorites.Contains(_draft.Transition);
            favorite.Content = isFavorite ? "★" : "☆";
            favorite.Background = isFavorite ? Lime : Brush("#303030");
            favorite.Foreground = isFavorite ? Brush("#171717") : Brush("#F5F5F5");
            favorite.IsEnabled = BibleTextTransitionCatalog.TryGet(_draft.Transition, out _) && (isFavorite || favorites.Count < 64);
            favorite.ToolTip = favorites.Count >= 64 && !isFavorite ? "Máximo 64 favoritos. Quita uno para añadir otro."
                : (isFavorite ? "Quitar de favoritos: " : "Añadir a favoritos: ") + name;
            AutomationProperties.SetName(favorite, favorite.ToolTip.ToString());
        }

        void FilterChoices()
        {
            updating = true;
            try
            {
                var query = NormalizeTransitionSearch(search.Text.Trim());
                var selectedFamily = family.SelectedValue as string ?? "";
                picker.Items.Clear();
                referencePicker.Items.Clear();
                referencePicker.Items.Add(new ComboBoxItem { Content = "Usar la misma del texto", Tag = "Same", ToolTip = "La referencia sigue el efecto elegido para el texto bíblico." });
                foreach (var choice in choices)
                {
                    if (selectedFamily.Length > 0 && choice.Family != selectedFamily) continue;
                    if (favoritesOnly.IsChecked == true && !favorites.Contains(choice.Id)) continue;
                    if (query.Length > 0 && !NormalizeTransitionSearch(choice.Name + " " + choice.Family).Contains(query, StringComparison.Ordinal)) continue;
                    picker.Items.Add(new ComboBoxItem { Content = choice.Name, ContentTemplate = picker.ItemTemplate, Tag = choice.Id, ToolTip = choice.Name });
                    if (choice.Id != "System")
                        referencePicker.Items.Add(new ComboBoxItem { Content = choice.Name, ContentTemplate = picker.ItemTemplate, Tag = choice.Id, ToolTip = choice.Name });
                }
                // Filtering changes the visible list, while the draft remains the selected effect.
                picker.SelectedValue = _draft.Transition;
                referencePicker.SelectedValue = _draft.ReferenceTransition ?? "Same";
                matches.Text = picker.Items.Count == 1 ? "1 efecto" : $"{picker.Items.Count} efectos";
                UpdateSelection();
            }
            finally { updating = false; }
        }

        void ToggleFavorite()
        {
            if (!BibleTextTransitionCatalog.TryGet(_draft.Transition, out _)) return;
            if (!favorites.Remove(_draft.Transition) && favorites.Count < 64) favorites.Add(_draft.Transition);
            _draft.FavoriteTransitions = string.Join("|", BibleTextTransitionCatalog.All.Where(preset => favorites.Contains(preset.Id)).Select(preset => preset.Id));
            FilterChoices();
        }

        picker.SelectionChanged += (_, _) =>
        {
            if (updating || picker.SelectedValue is not string id) return;
            _draft.Transition = id;
            UpdateSelection();
            UpdateDurationState();
        };
        referencePicker.SelectionChanged += (_, _) =>
        {
            if (updating || referencePicker.SelectedValue is not string id) return;
            _draft.ReferenceTransition = id == "Same" ? null : id;
            UpdateSelection();
            UpdateDurationState();
        };
        search.TextChanged += (_, _) => FilterChoices();
        family.SelectionChanged += (_, _) => FilterChoices();
        favorite.Click += (_, _) => ToggleFavorite();
        favoritesOnly.Checked += (_, _) => FilterChoices();
        favoritesOnly.Unchecked += (_, _) => FilterChoices();
        FilterChoices();
    }

    private static string NormalizeTransitionSearch(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(char.ToUpperInvariant(character));
        return result.ToString();
    }
}
