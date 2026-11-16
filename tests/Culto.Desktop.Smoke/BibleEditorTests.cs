using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunBibleEditorTests(MainWindow window)
    {
        var example = new Cue { Kind = CueKind.Bible, Title = "Ejemplo", Body = "El Señor es mi pastor; nada me faltará.", Reference = "Salmos 23:1", Version = "Ejemplo de prueba" };
        T Control<T>(BibleStyleDialog dialog, string name) where T : FrameworkElement => Descendants(dialog).OfType<T>().Single(control => control.Name == name);
        void Click(BibleStyleDialog dialog, string name) => Control<Button>(dialog, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        bool? Exercise(BibleStyleDialog dialog, Action<BibleStyleDialog> action)
        {
            Exception? error = null;
            dialog.Owner = window;
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try { dialog.UpdateLayout(); action(dialog); }
                catch (Exception ex) { error = ex; dialog.Close(); }
            }));
            var result = dialog.ShowDialog();
            if (error is not null) throw error;
            return result;
        }

        Test("Bible editor previews real controls and cancellation preserves source and live output", () =>
        {
            var original = new BibleVisualStyle();
            var unchanged = original with { };
            var session = Field<PresentationSession>(window, "_session");
            var live = session.Live; var prepared = session.Prepared;
            var mainPreviewStyle = Field<ProjectionSurface>(window, "_preview").BibleStyle;
            var dialog = new BibleStyleDialog(original, example);
            var result = Exercise(dialog, editor =>
            {
                Control<ComboBox>(editor, "BibleFontFamily").SelectedItem = "Arial";
                Control<Slider>(editor, "BibleFontSize").Value = 92;
                Control<TextBox>(editor, "BibleTextColor").Text = "#aabbcc";
                Control<CheckBox>(editor, "BibleBold").IsChecked = true;
                Control<ComboBox>(editor, "BibleTextEffect").SelectedValue = "Shadow";
                var preview = Field<ProjectionSurface>(editor, "_preview");
                Check(preview.BibleStyle.FontFamily == "Arial" && preview.BibleStyle.FontSize == 92, "Font controls did not update preview");
                Check(preview.BibleStyle.TextColor == "#AABBCC" && preview.BibleStyle.Bold && preview.BibleStyle.TextEffect == "Shadow", "Text controls did not update preview");
                Click(editor, "TestBibleTransitionButton");
                Check(preview.IsTransitioning, "Dissolve preview did not start");
                Until(() => !preview.IsTransitioning, "Editor preview transition did not complete");
                Check(!preview.IsTransitioning, "Dissolve preview did not finish");
                Check(ReferenceEquals(session.Live, live) && ReferenceEquals(session.Prepared, prepared), "Editor changed presentation session");
                Check(Field<ProjectionSurface>(window, "_preview").BibleStyle == mainPreviewStyle, "Editor changed main preview");
                Check(preview.GetType().GetField("_vlc", Private)!.GetValue(preview) is null, "Bible editor initialized VLC");
                Click(editor, "CancelBibleStyleButton");
            });
            Check(result == false && original == unchanged, "Cancel changed the supplied style");
            Check(Field<bool>(Field<ProjectionSurface>(dialog, "_preview"), "_disposed"), "Closing editor did not dispose preview");
        });

        Test("Bible editor saves normalized choices without mutating source", () =>
        {
            var original = new BibleVisualStyle();
            var dialog = new BibleStyleDialog(original, example);
            var result = Exercise(dialog, editor =>
            {
                Control<ComboBox>(editor, "BibleReferenceFontFamily").SelectedItem = "Georgia";
                Check(Field<TextBlock>(Field<ProjectionSurface>(editor, "_preview"), "_reference").FontFamily.Source == "Georgia",
                    "Reference font did not update independently in the preview");
                Check(Field<TextBlock>(Field<ProjectionSurface>(editor, "_preview"), "_body").FontFamily.Source == original.FontFamily,
                    "Reference font changed the passage font");
                Control<TextBox>(editor, "BibleBackgroundColor").Text = "#abcdef";
                Control<ComboBox>(editor, "BibleAlignment").SelectedValue = "Right";
                Control<ComboBox>(editor, "BibleTransition").SelectedValue = "Cut";
                Check(!Control<Slider>(editor, "BibleTransitionDuration").IsEnabled, "Cut retained an active duration slider");
                Click(editor, "SaveBibleStyleButton");
            });
            Check(result == true && dialog.EditedStyle.BackgroundColor == "#ABCDEF" && dialog.EditedStyle.Alignment == "Right" && dialog.EditedStyle.Transition == "Cut", "Save lost edited choices");
            Check(original.BackgroundColor == "#101722" && original.Alignment == "Center", "Editor mutated caller style before adoption");
        });

        Test("Bible editor groups controls beside a persistent preview and adapts to a small window", () =>
        {
            var dialog = new BibleStyleDialog(new BibleVisualStyle(), example);
            Exercise(dialog, editor =>
            {
                var sections = Control<ListBox>(editor, "BibleStyleSections");
                Check(sections.Items.Count == 4, "Editor needs Text, Reference, Background and Transition sections");
                foreach (var size in new[] { new Size(1180, 780), new Size(940, 640) })
                {
                    editor.Width = size.Width; editor.Height = size.Height;
                    foreach (var section in sections.Items.Cast<ListBoxItem>())
                    {
                        sections.SelectedItem = section;
                        if (section.Tag as string == "transicion")
                            Control<ComboBox>(editor, "BibleTransition").SelectedValue = BibleTextTransitionCatalog.All.First(preset =>
                                preset.Motion == BibleTextMotion.Rotate && preset.Scope == BibleTextScope.Words && preset.Variant == 4).Id;
                        editor.UpdateLayout(); PumpFor(40);
                        Check(Field<ProjectionSurface>(editor, "_preview").IsVisible, "Changing sections hid the preview");
                        var save = Control<Button>(editor, "SaveBibleStyleButton");
                        Check(save.IsVisible && save.TranslatePoint(new Point(), editor).Y + save.ActualHeight <= editor.ActualHeight,
                            "Small editor hid Save below the window");
                        if (section.Tag as string == "transicion")
                        {
                            var picker = Control<ComboBox>(editor, "BibleTransition");
                            var selected = Control<TextBlock>(editor, "BibleTransitionSelected");
                            Check(BibleTextTransitionCatalog.TryGet(picker.SelectedValue as string, out var preset) &&
                                selected.Text.Contains(preset.Name) && picker.ToolTip as string == preset.Name,
                                "Long preset lost its full selected name or tooltip");
                            Check(selected.TextWrapping == TextWrapping.Wrap && selected.ActualWidth <= ((FrameworkElement)selected.Parent).ActualWidth,
                                "Long selected name does not wrap within the compact editor");
                            var duration = Control<Slider>(editor, "BibleTransitionDuration");
                            var scroll = Descendants(editor).OfType<ScrollViewer>().Single(view => Descendants(view).Contains(duration));
                            Check(duration.IsEnabled && duration.TranslatePoint(new Point(), scroll).Y + duration.ActualHeight <= scroll.ViewportHeight + 1,
                                "Long preset hid the duration below the small editor viewport");
                        }
                        CaptureDesignWindow(editor, System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "bible-editor-" + section.Tag + "-" + (int)size.Width + ".png"));
                    }
                }
                Click(editor, "CancelBibleStyleButton");
            });
        });

        Test("Bible editor offers and previews all three text transitions with adjustable duration", () =>
        {
            var dialog = new BibleStyleDialog(new BibleVisualStyle(), example);
            Exercise(dialog, editor =>
            {
                Control<ListBox>(editor, "BibleStyleSections").SelectedIndex = 3;
                var picker = Control<ComboBox>(editor, "BibleTransition");
                foreach (var kind in new[] { "TextRise", "TextLines", "TextFocus" })
                {
                    picker.SelectedValue = kind;
                    Check(picker.SelectedValue as string == kind, "Missing text transition: " + kind);
                    var duration = Control<Slider>(editor, "BibleTransitionDuration");
                    Check(duration.IsEnabled, "Text transition duration is disabled");
                    duration.Value = 300;
                    Check(editor.EditedStyle.Transition == kind && editor.EditedStyle.TransitionMilliseconds == 300,
                        "Editor lost text transition settings");
                    Click(editor, "TestBibleTransitionButton");
                    var preview = Field<ProjectionSurface>(editor, "_preview");
                    Check(preview.IsTransitioning, "Text transition did not start in the editor: " + kind);
                    Until(() => !preview.IsTransitioning, "Text transition preview did not complete: " + kind);
                }
                Click(editor, "SaveBibleStyleButton");
            });
            Check(dialog.EditedStyle.Transition == "TextFocus", "Save lost the selected text transition");
        });

        Test("Bible transition catalog searches without accents and preserves the selected effect through filters", () =>
        {
            var selected = BibleTextTransitionCatalog.All.First(preset => preset.Motion == BibleTextMotion.Slide && preset.Id.StartsWith("TextPreset:"));
            var dialog = new BibleStyleDialog(new BibleVisualStyle { Transition = selected.Id }, example);
            Exercise(dialog, editor =>
            {
                Control<ListBox>(editor, "BibleStyleSections").SelectedIndex = 3;
                var picker = Control<ComboBox>(editor, "BibleTransition");
                var search = Control<TextBox>(editor, "BibleTransitionSearch");
                var family = Control<ComboBox>(editor, "BibleTransitionFamily");
                Check(picker.SelectedValuePath == "Tag" && picker.Items.Count > 200, "Catalog did not populate the existing transition picker");
                Check(picker.SelectedValue as string == selected.Id, "Catalog did not restore the selected effect");
                foreach (var preset in BibleTextTransitionCatalog.All)
                {
                    picker.SelectedValue = preset.Id;
                    Check(editor.EditedStyle.Transition == preset.Id && Control<Slider>(editor, "BibleTransitionDuration").IsEnabled,
                        "Catalog selection or duration failed: " + preset.Id);
                }
                picker.SelectedValue = selected.Id;
                search.Text = "ROTACION";
                Check(picker.Items.Count > 0 && picker.Items.Cast<ComboBoxItem>().All(item =>
                    BibleTextTransitionCatalog.TryGet(item.Tag as string, out var preset) && preset.Motion == BibleTextMotion.Rotate),
                    "Accent-insensitive search failed to find rotation effects");
                Check(editor.EditedStyle.Transition == selected.Id, "Search changed the selected transition");
                Check(Control<TextBlock>(editor, "BibleTransitionSelected").Text.Contains(selected.Name), "Filtered selection lost its visible name");
                search.Text = "no existe este efecto";
                Check(picker.Items.Count == 0 && editor.EditedStyle.Transition == selected.Id, "Empty search discarded the draft selection");
                Check(Control<TextBlock>(editor, "BibleTransitionMatches").Text.StartsWith("0 "), "Empty search did not report zero matches");
                search.Clear();
                Check(picker.SelectedValue as string == selected.Id, "Clearing the search did not restore the selected item");
                family.SelectedValue = selected.Family;
                Check(picker.Items.Count == BibleTextTransitionCatalog.All.Count(preset => preset.Family == selected.Family), "Family filter count is incorrect");
                Check(picker.Items.Cast<ComboBoxItem>().All(item => BibleTextTransitionCatalog.TryGet(item.Tag as string, out var preset) && preset.Family == selected.Family),
                    "Family filter included another family");
                family.SelectedIndex = 0;
                Check(picker.Items.Count == BibleTextTransitionCatalog.All.Count + 3, "Clearing filters did not restore the full catalog");
                Click(editor, "CancelBibleStyleButton");
            });
        });

        Test("Bible transition favorites save and reopen with the chosen effect", () =>
        {
            var selected = BibleTextTransitionCatalog.All.First(preset => preset.Id.StartsWith("TextPreset:"));
            var original = new BibleVisualStyle();
            var dialog = new BibleStyleDialog(original, example);
            Check(Exercise(dialog, editor =>
            {
                Control<ComboBox>(editor, "BibleTransition").SelectedValue = selected.Id;
                Click(editor, "BibleTransitionFavorite");
                Control<CheckBox>(editor, "BibleTransitionFavoritesOnly").IsChecked = true;
                var picker = Control<ComboBox>(editor, "BibleTransition");
                Check(picker.Items.Count == 1 && picker.SelectedValue as string == selected.Id, "Favorite-only filter lost the selected favorite");
                Control<Slider>(editor, "BibleTransitionDuration").Value = 550;
                Click(editor, "SaveBibleStyleButton");
            }) == true, "Favorite editor did not save");
            Check(string.IsNullOrEmpty(original.FavoriteTransitions), "Saving favorites mutated the incoming style");
            var saved = dialog.EditedStyle;
            Check(saved.FavoriteTransitions == selected.Id && saved.Transition == selected.Id && saved.TransitionMilliseconds == 550, "Save lost favorite or transition settings");
            var reopened = new BibleStyleDialog(saved, example);
            Exercise(reopened, editor =>
            {
                Check(Control<ComboBox>(editor, "BibleTransition").SelectedValue as string == selected.Id, "Reopening lost the selected transition");
                Control<CheckBox>(editor, "BibleTransitionFavoritesOnly").IsChecked = true;
                Check(Control<ComboBox>(editor, "BibleTransition").Items.Count == 1, "Reopening lost the saved favorite");
                Click(editor, "BibleTransitionFavorite");
                Check(Control<ComboBox>(editor, "BibleTransition").Items.Count == 0 && editor.EditedStyle.Transition == selected.Id,
                    "Removing a filtered favorite discarded the selection");
                Click(editor, "CancelBibleStyleButton");
            });
            Check(saved.FavoriteTransitions == selected.Id, "Cancel removed a saved favorite from the source style");
        });

        Test("Bible transition favorite changes are isolated until save", () =>
        {
            var preset = BibleTextTransitionCatalog.All.First();
            var original = new BibleVisualStyle { Transition = preset.Id };
            var before = original with { };
            var dialog = new BibleStyleDialog(original, example);
            var result = Exercise(dialog, editor =>
            {
                Click(editor, "BibleTransitionFavorite");
                Check(editor.EditedStyle.FavoriteTransitions == preset.Id, "Favorite button did not update the draft");
                Control<TextBox>(editor, "BibleTransitionSearch").Text = "sin resultados";
                Check(editor.EditedStyle.Transition == preset.Id, "Search changed the draft while editing favorites");
                Click(editor, "CancelBibleStyleButton");
            });
            Check(result == false && original == before, "Cancel changed the supplied transition or favorites");
        });

        Test("Bible editor invalid color preserves editor and remains cancellable", () =>
        {
            var dialog = new BibleStyleDialog(new BibleVisualStyle(), example);
            var result = Exercise(dialog, editor =>
            {
                Control<TextBox>(editor, "BibleTextColor").Text = "#NOPE";
                Control<ListBox>(editor, "BibleStyleSections").SelectedIndex = 2;
                Click(editor, "SaveBibleStyleButton");
                Check(editor.IsVisible && editor.DialogResult != true, "Invalid color closed editor");
                Check(Control<TextBox>(editor, "BibleTextColor").IsVisible, "Validation did not reveal the section with the invalid color");
                Check(Field<ProjectionSurface>(editor, "_preview").BibleStyle.TextColor == "#FFFFFF", "Invalid color destroyed preview");
                Check(Field<TextBlock>(editor, "_status").Text.Length > 0, "Invalid color lacked a visible explanation");
                Click(editor, "CancelBibleStyleButton");
            });
            Check(result == false, "Invalid color blocked cancellation");
        });

        Test("Bible editor missing image falls back without changing incoming style", () =>
        {
            var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
            var original = new BibleVisualStyle { BackgroundImagePath = missing };
            var dialog = new BibleStyleDialog(original, example);
            Exercise(dialog, editor =>
            {
                Check(editor.EditedStyle.BackgroundImagePath is null && Field<TextBlock>(editor, "_status").Text.Length > 0, "Missing image did not display fallback status");
                Click(editor, "CancelBibleStyleButton");
            });
            Check(original.BackgroundImagePath == missing, "Missing-image fallback mutated source");
        });
    }
}
