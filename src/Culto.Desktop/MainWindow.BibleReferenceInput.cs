using System.Windows.Input;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void InitializeBibleReferenceInput()
    {
        BibleQuery.GotKeyboardFocus += (_, e) =>
        {
            if (ReferenceEquals(e.NewFocus, BibleQuery)) SelectBibleQueryBook();
        };
        BibleQuery.PreviewMouseLeftButtonDown += BibleReferenceMouseDown;
        BibleQuery.PreviewKeyDown += BibleReferenceKeyDown;
    }

    // Used by Ctrl+F as well as ordinary keyboard focus. A phrase retains normal editing.
    private bool SelectBibleQueryBook()
    {
        if (!BibleReferenceInput.TryParse(BibleQuery.Text, out var reference)) return false;
        var book = reference.Segments[0];
        BibleQuery.Select(book.Start, book.Length);
        return true;
    }

    private void BibleReferenceMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Modifier-assisted selection and caret movement keep standard TextBox behavior.
        if (Keyboard.Modifiers != ModifierKeys.None || !BibleReferenceInput.TryParse(BibleQuery.Text, out var reference)) return;
        var character = BibleQuery.GetCharacterIndexFromPoint(e.GetPosition(BibleQuery), snapToText: true);
        if (character < 0) return;
        BibleQuery.Focus();
        var segment = reference.SegmentAt(character);
        BibleQuery.Select(segment.Start, segment.Length);
        e.Handled = true;
    }

    private void BibleReferenceKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Tab || (Keyboard.Modifiers & ~ModifierKeys.Shift) != ModifierKeys.None
            || !BibleReferenceInput.TryParse(BibleQuery.Text, out var reference)) return;
        var current = reference.SegmentAt(BibleQuery.SelectionStart);
        var index = reference.Segments.ToList().FindIndex(segment => segment.Part == current.Part);
        var next = index + (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
        // At either end, allow WPF's normal Tab traversal to leave the editor.
        if (next < 0 || next >= reference.Segments.Count) return;
        var segment = reference.Segments[next];
        BibleQuery.Select(segment.Start, segment.Length);
        e.Handled = true;
    }
}
