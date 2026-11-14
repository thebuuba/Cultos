using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunSimpleConsoleTests(MainWindow window)
    {
        Test("Console omits progress and notes controls while order selection still prepares content", () =>
        {
            foreach (var name in new[] { "HeaderProgress", "HeaderProgressText", "NotesToggleButton", "NotesPanel", "NotesBox" })
                Check(window.FindName(name) is null, "Removed control still exists: " + name);
            var plan = (ListBox)window.FindName("PlanList");
            plan.SelectedIndex = -1;
            plan.SelectedIndex = 0;
            Check(Field<PresentationSession>(window, "_session").Prepared?.Id == ((Cue)plan.SelectedItem).Id,
                "Removing notes broke order selection");
        });
    }
}
