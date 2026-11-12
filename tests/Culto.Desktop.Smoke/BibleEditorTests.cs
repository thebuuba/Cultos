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
                Control<TextBox>(editor, "BibleBackgroundColor").Text = "#abcdef";
                Control<ComboBox>(editor, "BibleAlignment").SelectedValue = "Right";
                Control<ComboBox>(editor, "BibleTransition").SelectedValue = "Cut";
                Check(!Control<Slider>(editor, "BibleTransitionDuration").IsEnabled, "Cut retained an active duration slider");
                Click(editor, "SaveBibleStyleButton");
            });
            Check(result == true && dialog.EditedStyle.BackgroundColor == "#ABCDEF" && dialog.EditedStyle.Alignment == "Right" && dialog.EditedStyle.Transition == "Cut", "Save lost edited choices");
            Check(original.BackgroundColor == "#101722" && original.Alignment == "Center", "Editor mutated caller style before adoption");
        });

        Test("Bible editor invalid color preserves editor and remains cancellable", () =>
        {
            var dialog = new BibleStyleDialog(new BibleVisualStyle(), example);
            var result = Exercise(dialog, editor =>
            {
                Control<TextBox>(editor, "BibleTextColor").Text = "#NOPE";
                Click(editor, "SaveBibleStyleButton");
                Check(editor.IsVisible && editor.DialogResult != true, "Invalid color closed editor");
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
