using System.Collections.ObjectModel;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunSavedSetsTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var plan = Field<ObservableCollection<Cue>>(window, "_plan");
        var session = Field<PresentationSession>(window, "_session");
        var name = (TextBox)window.FindName("PlanNameBox");
        var oldPlan = plan.ToList(); var oldName = name.Text;
        var oldCollections = workspace.VideoCollections; var oldServices = workspace.SavedServices;
        try
        {
            Test("Saved collections and services survive workspace reload with sequence and notes", () =>
            {
                workspace.VideoCollections = [SavedSet.Capture("Alabanza sintética", [new Cue
                    { Title = "Video", Kind = CueKind.Video, FilePath = "synthetic-video.mp4", Notes = "Entrada" }])];
                workspace.SavedServices = [SavedSet.Capture("Culto sintético", [new Cue { Title = "Bienvenida" },
                    new Cue { Title = "Lectura", Kind = CueKind.Bible, Body = "Pasaje sintético" }])];
                Check((bool)Call(window, "SaveWorkspace")!, "Workspace save failed");
                var loaded = new WorkspaceStore().Load();
                Check(loaded.VideoCollections.Single().Items.Single().Notes == "Entrada", "Video collection notes were lost");
                Check(loaded.SavedServices.Single().Items.Select(c => c.Title).SequenceEqual(new[] { "Bienvenida", "Lectura" }),
                    "Saved worship order was lost");
            });

            Test("Adding and replacing saved orders uses fresh cue IDs without changing live output", () =>
            {
                var live = session.Live;
                var set = SavedSet.Capture("Plantilla sintética", [new Cue { Title = "Primero", Notes = "Nota" }, new Cue { Title = "Segundo" }]);
                var before = plan.Count; var originalIds = plan.Select(c => c.Id).ToArray();
                Check((bool)Call(window, "ApplySavedSet", set, false)!, "Adding saved set failed");
                Check(plan.Count == before + 2 && plan.Take(before).Select(c => c.Id).SequenceEqual(originalIds), "Append altered the original order");
                var appended = plan.Skip(before).ToArray();
                Check(appended.Select(c => c.Title).SequenceEqual(new[] { "Primero", "Segundo" }), "Append reordered saved cues");
                Check(appended.All(c => set.Items.All(saved => saved.Id != c.Id)), "Append reused saved cue IDs");
                appended[0].Notes = "Modificada";
                Check(set.Items[0].Notes == "Nota", "Order edit changed the saved template");
                Check(ReferenceEquals(session.Live, live), "Appending changed live output");
                Check((bool)Call(window, "ApplySavedSet", set, true)!, "Replacing with saved set failed");
                Check(plan.Count == 2 && name.Text == set.Name && plan[0].Notes == "Nota", "Replacement did not restore saved order and notes");
                Check(plan.All(c => appended.All(previous => previous.Id != c.Id)), "Repeated load reused an active cue ID");
                Check(ReferenceEquals(session.Live, live), "Replacing changed live output");
                var loaded = new WorkspaceStore().Load();
                Check(loaded.Plan.Name == set.Name && loaded.Plan.Items.Count == 2, "Replacement did not persist");
            });
        }
        finally
        {
            plan.Clear(); foreach (var cue in oldPlan) plan.Add(cue); name.Text = oldName;
            workspace.VideoCollections = oldCollections; workspace.SavedServices = oldServices;
            Call(window, "UpdatePlanCount"); Call(window, "SaveWorkspace");
        }
    }
}
