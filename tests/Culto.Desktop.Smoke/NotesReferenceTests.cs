using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunNotesReferenceTests(MainWindow window)
    {
        Test("Notes exposes categories, pinning, private moderator and explicit projection", () =>
        {
            foreach (var name in new[] { "NotesFilters", "NotesCategoryBox", "NotesPinnedCheck", "NotesModeratorCheck", "NotesProjectButton" })
                Check(window.FindName(name) is FrameworkElement, "Missing reference notes control: " + name);
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var original = workspace.Notes.ToList();
            var session = Field<PresentationSession>(window, "_session");
            var live = session.Live;
            Call(window, "NewNoteClick", window, new RoutedEventArgs());
            ((TextBox)window.FindName("NotesTitleBox")).Text = "Peticiones de oración";
            ((TextBox)window.FindName("NotesBodyBox")).Text = "Familia López\nConservar esta segunda línea";
            ((ComboBox)window.FindName("NotesCategoryBox")).SelectedValue = "oracion";
            var pin = (CheckBox)window.FindName("NotesPinnedCheck"); pin.IsChecked = true; pin.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            var moderator = (CheckBox)window.FindName("NotesModeratorCheck"); moderator.IsChecked = true; moderator.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            Call(window, "NotesBackClick", window, new RoutedEventArgs());
            var store = Field<WorkspaceStore>(window, "_store");
            var saved = store.Load().Notes.Single(n => n.Title == "Peticiones de oración");
            Check((string)saved.GetType().GetProperty("Category")!.GetValue(saved)! == "oracion"
                && (bool)saved.GetType().GetProperty("IsPinned")!.GetValue(saved)!, "Note category/pin did not persist");
            Check(ReferenceEquals(session.Live, live), "Editing private notes changed the output");
            var list = (ListBox)window.FindName("NotesList");
            Check(ReferenceEquals(list.Items[0], workspace.Notes.Single(n => n.Id == saved.Id)), "Pinned note is not first");
            Call(window, "SetNotesFilter", "sermon");
            Check(!list.Items.Cast<WorkspaceNote>().Any(n => n.Id == saved.Id), "Category filter did not apply");
            Call(window, "SetNotesFilter", "oracion");
            Check(list.Items.Count == 1, "Prayer filter lost the note");
            list.SelectedIndex = 0;
            Call(window, "ProjectNoteClick", window, new RoutedEventArgs());
            Check(session.Live?.Id == saved.Id && session.Live.Kind == CueKind.Text && session.Live.Body.Contains("segunda línea"), "Explicit note projection failed");
            Call(window, "CloseOutput");
            workspace.Notes = original; Call(window, "ShowNotesList"); Call(window, "SetNotesFilter", "all");
        });
        Test("Reference notes cards keep four categories and pinned groups visible", () =>
        {
            var workspace = Field<WorkspaceData>(window, "_workspace"); var original = workspace.Notes;
            workspace.Notes = [
                new() { Title = "Bosquejo · Fe verdadera", Body = "1. La fe es certeza (Heb 11:1)\n2. La fe actúa (Stg 2:17)…", Category = "sermon", IsPinned = true, ModifiedAt = DateTimeOffset.Now.AddMinutes(-5) },
                new() { Title = "Anuncios", Body = "• Ayuno congregacional el viernes 6:00 a. m.\n• Inscripciones del campamento hasta el 30…", Category = "anuncio", ModifiedAt = DateTimeOffset.Now.AddHours(-1) },
                new() { Title = "Peticiones de oración", Body = "Hna. Marta (salud), familia López (trabajo), viaje misionero a Oaxaca.", Category = "oracion", ShowInModerator = false, ModifiedAt = DateTimeOffset.Now.AddHours(-2) },
                new() { Title = "Técnico: cambiar micrófono", Body = "Después de la alabanza, pasar al micrófono de diadema del pastor y bajar la música de fondo al 30%.", Category = "recordatorio", ShowInModerator = false, ModifiedAt = DateTimeOffset.Now.AddDays(-1) }
            ];
            Call(window, "ActivateWorkspacePanel", "text"); Call(window, "ShowNotesList"); window.UpdateLayout();
            Check(((ListBox)window.FindName("NotesList")).Items.Count == 4, "Reference cards were lost");
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "notes-reference-cards.png"));
            var liveBefore = Field<PresentationSession>(window, "_session").Live;
            Call(window, "OpenNotesModeratorClick", window, new RoutedEventArgs());
            var privateText = Field<TextBlock>(window, "_notesModeratorBody").Text;
            Check(privateText.Contains("Bosquejo") && privateText.Contains("Anuncios") && !privateText.Contains("Peticiones")
                && !privateText.Contains("Técnico"), "Private moderator ignored the eye setting");
            Check(ReferenceEquals(liveBefore, Field<PresentationSession>(window, "_session").Live), "Opening private moderator projected notes");
            Field<Window>(window, "_notesModerator").Close();
            workspace.Notes = original; Call(window, "RefreshNotes");
        });
        Test("USB presenter uses a compact trigger and a separate popover", () =>
        {
            Check(window.FindName("PowerPointUsbOptions") is ToggleButton, "USB presenter is still an inline expander");
            Check(window.FindName("PowerPointUsbPopup") is Popup, "USB presenter popover is missing");
            Call(window, "ActivateWorkspacePanel", "powerpoint"); window.UpdateLayout();
            ((ToggleButton)window.FindName("PowerPointUsbOptions")).IsChecked = true;
            window.UpdateLayout(); PumpFor(50);
            Check(((Popup)window.FindName("PowerPointUsbPopup")).IsOpen, "USB trigger did not open its popover");
            Check(((FrameworkElement)window.FindName("PowerPointUsbKeys")).IsVisible, "USB key selection is not visible in the popover");
            var popupContent = (FrameworkElement)((Popup)window.FindName("PowerPointUsbPopup")).Child;
            popupContent.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(popupContent.ActualWidth), (int)Math.Ceiling(popupContent.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(popupContent); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(Environment.CurrentDirectory, ".tools", "usb-popover-reference.png"))) encoder.Save(stream);
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "usb-notes-reference.png"));
            ((ToggleButton)window.FindName("PowerPointUsbOptions")).IsChecked = false;
        });
    }
}
