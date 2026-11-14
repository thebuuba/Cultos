using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunPowerPointRefreshTests(MainWindow window, string directory)
    {
        Test("Hidden PowerPoint list defers library and order refreshes until activation", () =>
        {
            var layout = (string)Call(window, "SerializeDockLayout")!;
            var files = (ListBox)window.FindName("PowerPointFiles");
            var plan = Field<ObservableCollection<Cue>>(window, "_plan");
            var library = Field<Culto.Desktop.Services.WorkspaceData>(window, "_workspace").Library;
            var retained = new Cue { Kind = CueKind.PowerPoint, Title = "Refresh retained", FilePath = Path.Combine(directory, "retained.pptx") };
            var imported = new Cue { Kind = CueKind.PowerPoint, Title = "Refresh imported", FilePath = Path.Combine(directory, "imported.pptx") };
            var ordered = new Cue { Kind = CueKind.PowerPoint, Title = "Refresh ordered", FilePath = Path.Combine(directory, "ordered.pptx") };
            var changes = 0;
            NotifyCollectionChangedEventHandler changed = (_, _) => changes++;
            var collection = Field<ObservableCollection<Cue>>(window, "_powerPointFiles");
            try
            {
                library.Add(retained);
                Call(window, "ActivateWorkspacePanel", "powerpoint"); window.UpdateLayout();
                Call(window, "RefreshPowerPointFiles");
                files.SelectedItem = retained;
                Call(window, "ActivateWorkspacePanel", "sources"); window.UpdateLayout();
                Check(!((FrameworkElement)window.FindName("PowerPointPanel")).IsVisible, "PowerPoint tab remained visible");
                collection.CollectionChanged += changed;
                library.Add(imported);
                Call(window, "RefreshLibrary");
                plan.Add(ordered);
                Call(window, "RefreshLibrary");
                Check(changes == 0, "Hidden PowerPoint list was rebuilt during library/order updates");
                Call(window, "ActivateWorkspacePanel", "powerpoint"); window.UpdateLayout();
                Check(files.Items.Contains(imported) && files.Items.Contains(ordered), "Activating PowerPoint did not catch up with hidden changes");
                Check(ReferenceEquals(files.SelectedItem, retained), "Deferred refresh lost the selected presentation");
                library.Remove(imported); plan.Remove(ordered);
                Call(window, "RefreshLibrary");
                Check(!files.Items.Contains(imported) && !files.Items.Contains(ordered), "Visible PowerPoint list did not reflect removals");
            }
            finally
            {
                collection.CollectionChanged -= changed;
                library.Remove(retained); library.Remove(imported); plan.Remove(ordered);
                Call(window, "RestoreDockLayout", layout); window.UpdateLayout();
                Call(window, "RefreshLibrary");
            }
        });
    }
}
