using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunBibleStylePersistenceTests(MainWindow window)
    {
        var example = new Cue { Kind = CueKind.Bible, Body = "Texto", Reference = "Salmos 23:1" };
        T Control<T>(BibleStyleDialog dialog, string name) where T : FrameworkElement =>
            Descendants(dialog).OfType<T>().Single(control => control.Name == name);
        void Click(BibleStyleDialog dialog, string name) =>
            Control<Button>(dialog, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        bool? Exercise(BibleStyleDialog dialog, Action<BibleStyleDialog> action)
        {
            Exception? error = null;
            dialog.Owner = window;
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try { dialog.UpdateLayout(); action(dialog); }
                catch (Exception ex) { error = ex; dialog.DialogResult = false; }
            }));
            var result = dialog.ShowDialog();
            if (error is not null) throw error;
            return result;
        }

        Test("Closing the Bible editor cannot silently discard the reference color", () =>
        {
            var dialog = new BibleStyleDialog(new BibleVisualStyle(), example);
            Check(Exercise(dialog, editor =>
            {
                Control<CheckBox>(editor, "BibleReferenceSameColor").IsChecked = false;
                Control<TextBox>(editor, "BibleReferenceColor").Text = "#A7C7FF";
                editor.Close();
                Check(editor.IsVisible, "Closing with X silently discarded the reference color");
                Check(Field<TextBlock>(editor, "_status").Text.Contains("Guardar"), "Close protection must explain how to save");
                Click(editor, "SaveBibleStyleButton");
            }) == true, "The protected draft could not be saved");
        });

        Test("Bible editor saves every style setting to disk and reopens the independent reference color", () =>
        {
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var previous = workspace.BibleStyle;
            var style = new BibleVisualStyle { FontFamily = "Arial", ReferenceFontFamily = "Georgia", FontSize = 88,
                TextColor = "#112233", ReferenceColor = "#FFE08A", ShowVersion = false, Bold = true, Italic = true,
                Alignment = "Right", TextEffect = "Shadow", BackgroundColor = "#334455", BackgroundDim = .6,
                Transition = "Dissolve", ReferenceTransition = "Cut", TransitionMilliseconds = 850 };
            try
            {
                var dialog = new BibleStyleDialog(style, example);
                Check(Exercise(dialog, editor =>
                {
                    Control<TextBox>(editor, "BibleReferenceColor").Text = "#a7c7ff";
                    Control<ListBox>(editor, "BibleStyleSections").SelectedIndex = 0;
                    Control<TextBox>(editor, "BibleTextColor").Text = "#445566";
                    Click(editor, "SaveBibleStyleButton");
                }) == true, "Editor did not accept valid settings");
                Check((bool)Call(window, "ApplyBibleStyle", dialog.EditedStyle)!, "Could not persist editor settings");
                var loaded = new WorkspaceStore().Load().BibleStyle;
                Check(loaded == (style with { ReferenceColor = "#A7C7FF", TextColor = "#445566" }), "A style setting was lost on disk");
                Exercise(new BibleStyleDialog(loaded, example), editor =>
                {
                    Check(Control<CheckBox>(editor, "BibleReferenceSameColor").IsChecked == false
                        && Control<TextBox>(editor, "BibleReferenceColor").Text == "#A7C7FF", "Reopening lost the independent color");
                    Click(editor, "CancelBibleStyleButton");
                });
            }
            finally { Call(window, "ApplyBibleStyle", previous); }
        });

        Test("A storage failure keeps the Bible draft open and allows saving it again", () =>
        {
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var previous = workspace.BibleStyle;
            var blocked = Field<bool>(window, "_workspaceRecoveryBlocked");
            try
            {
                var dialog = new BibleStyleDialog(previous, example)
                { PersistStyle = style => (bool)Call(window, "ApplyBibleStyle", style)! };
                Check(Exercise(dialog, editor =>
                {
                    Control<CheckBox>(editor, "BibleReferenceSameColor").IsChecked = false;
                    Control<TextBox>(editor, "BibleReferenceColor").Text = "#A1B2C3";
                    SetField(window, "_workspaceRecoveryBlocked", true);
                    Click(editor, "SaveBibleStyleButton");
                    Check(editor.IsVisible && editor.EditedStyle.ReferenceColor == "#A1B2C3",
                        "A failed save closed the editor or discarded the draft");
                    Check(workspace.BibleStyle == previous && Field<TextBlock>(editor, "_status").Text.Contains("No se pudo guardar"),
                        "A failed save was reported as successful");
                    SetField(window, "_workspaceRecoveryBlocked", false);
                    Click(editor, "SaveBibleStyleButton");
                }) == true, "Retry did not save the draft");
                Check(new WorkspaceStore().Load().BibleStyle.ReferenceColor == "#A1B2C3", "Retry lost the reference color");
            }
            finally
            {
                SetField(window, "_workspaceRecoveryBlocked", blocked);
                Call(window, "ApplyBibleStyle", previous);
            }
        });

        Test("An unchanged Bible editor closes normally", () =>
            Check(Exercise(new BibleStyleDialog(new BibleVisualStyle(), example), editor => editor.Close()) != true,
                "An unchanged editor should close without saving"));
    }
}
