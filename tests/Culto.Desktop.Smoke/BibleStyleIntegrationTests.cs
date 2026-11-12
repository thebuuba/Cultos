using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunBibleStyleIntegrationTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var session = Field<PresentationSession>(window, "_session");
        var preview = Field<ProjectionSurface>(window, "_preview");
        var plan = Field<ObservableCollection<Cue>>(window, "_plan");
        var oldStyle = workspace.BibleStyle;
        var oldPlan = plan.ToArray();
        var verse = new Cue { Kind = CueKind.Bible, Title = "Prueba 1:1", Reference = "Prueba 1:1",
            Body = "Texto bíblico para verificar el estilo guardado.", Version = "Versión de prueba" };
        try
        {
            Test("Bible style saves independently and updates only the prepared view until projection", () =>
            {
                Call(window, "ApplyBibleStyle", new BibleVisualStyle { FontFamily = "Segoe UI", Transition = "Cut" });
                Call(window, "PrepareCue", verse, null);
                Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                var live = session.Live;
                var prepared = session.Prepared;
                var style = new BibleVisualStyle { FontFamily = "Georgia", FontSize = 88, TextColor = "#FFC080",
                    BackgroundColor = "#243850", Bold = true, Italic = true, Alignment = "Left", TextEffect = "Shadow", Transition = "Cut" };
                Check((bool)Call(window, "ApplyBibleStyle", style)!, "Saving Bible style failed");
                Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Style editing changed cue identities");
                Check(Field<TextBlock>(preview, "_body").FontFamily.Source == "Georgia", "Prepared Bible did not adopt saved font");
                Check(Field<TextBlock>(output, "_body").FontFamily.Source == "Segoe UI", "Saving style changed the projected verse prematurely");
                style.FontFamily = "Arial";
                Check(workspace.BibleStyle.FontFamily == "Georgia", "Saved style is aliased to the editor's mutable draft");
                var loaded = new WorkspaceStore().Load().BibleStyle;
                Check(loaded.FontFamily == "Georgia" && loaded.Bold && loaded.Italic && loaded.TextEffect == "Shadow"
                    && loaded.TextColor == "#FFC080" && loaded.BackgroundColor == "#243850", "Saved workspace lost Bible style fields");
                Call(window, "ProjectPrepared");
                Check(Field<TextBlock>(output, "_body").FontFamily.Source == "Georgia" && !output.IsTransitioning,
                    "Explicit projection did not apply saved style with Cut");
            });

            Test("Bible navigation uses the saved transition and ordinary texts retain their own appearance", () =>
            {
                var next = new Cue { Kind = CueKind.Bible, Title = "Prueba 1:2", Body = "Segundo versículo", Reference = "Prueba 1:2" };
                plan.Clear(); plan.Add(verse); plan.Add(next); Call(window, "UpdatePlanCount");
                Call(window, "ApplyBibleStyle", workspace.BibleStyle with { Transition = "Dissolve", TransitionMilliseconds = 150 });
                Call(window, "PrepareCue", verse, null); Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                Until(() => !output.IsTransitioning, "Initial Bible transition did not finish");
                Call(window, "MoveLive", 1);
                Check(session.Live?.Id == next.Id && output.IsTransitioning, "Next verse bypassed configured Bible transition");
                Until(() => !output.IsTransitioning, "Next Bible transition did not finish");
                Check(Field<TextBlock>(output, "_body").FontFamily.Source == "Georgia", "Navigation lost Bible font");
                var plain = new Cue { Title = "Anuncio", Body = "Texto normal" };
                Call(window, "PrepareCue", plain, null); Call(window, "ProjectPrepared");
                var body = Field<TextBlock>(output, "_body");
                Check(body.FontFamily.Source == "Segoe UI" && body.FontWeight == FontWeights.Normal
                    && body.FontStyle == FontStyles.Normal && body.Effect is null, "Bible font effects leaked into ordinary text");
            });
        }
        finally
        {
            workspace.BibleStyle = oldStyle;
            plan.Clear(); foreach (var cue in oldPlan) plan.Add(cue);
            Call(window, "UpdatePlanCount");
            Call(window, "PrepareCue", new Cue { Title = "Fin de prueba", Body = "Fin" }, null);
            Call(window, "ProjectPrepared"); Call(window, "SaveWorkspace");
        }
    }
}
