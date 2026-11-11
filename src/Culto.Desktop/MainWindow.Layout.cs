using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Culto.Desktop;

public partial class MainWindow
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var handle = new WindowInteropHelper(this).Handle;
        var enabled = 1;
        var caption = 0x201E19;
        var text = 0xF4F6F5;
        // Native caption buttons remain managed by Windows.
        DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private void ContentTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, ContentTabs) && ContentTabs.SelectedItem is TabItem { Content: FrameworkElement content })
            UiMotion.Reveal(content);
    }

    private void OpenActionsClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ToggleNotesClick(object sender, RoutedEventArgs e)
    {
        if (FindName("NotesPanel") is not FrameworkElement panel || FindName("NotesToggleButton") is not Button toggle) return;
        var opening = panel.Visibility != Visibility.Visible;
        panel.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        toggle.Content = opening ? "Ocultar" : "Notas";
        if (opening) NotesBox.Focus();
        else CommitNotes();
    }
}
