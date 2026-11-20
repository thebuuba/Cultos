using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunPowerPointHandoffTests(MainWindow window, string directory)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        Test("New and existing workspaces have a dynamic PowerPoint scene", () =>
        {
            Check(SceneTile.Defaults().Any(s => s.Action == "powerpoint" && s.Cue is null), "Missing dynamic PowerPoint scene");
            Check(workspace.SceneTiles.Any(s => s.Action == "powerpoint" && s.Cue is null), "Existing workspace did not gain its PowerPoint scene");
        });
        if (!PowerPointPresentation.IsAvailable) return;
        var path = Path.Combine(directory, "handoff.pptx");
        dynamic application = Activator.CreateInstance(Type.GetTypeFromProgID("PowerPoint.Application")!)!;
        dynamic presentations = application.Presentations;
        try { CreatePowerPointFixture((object)presentations, path, null); }
        finally { ReleasePowerPointTestCom((object)presentations); ReleasePowerPointTestCom((object)application); }
        var cue = MediaLibrary.FromFile(path);
        workspace.Transitions = new TransitionOptions { Kind = "Slide", DurationMilliseconds = 900, AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
        ((Slider)window.FindName("VolumeSlider")).Value = 0;
        var session = Field<PresentationSession>(window, "_session");
        PowerPointPresentation? owner = null;
        Test("PowerPoint transitions to slide pixels with the active system transition", () =>
        {
            Call(window, "PrepareCue", new Cue { Kind = CueKind.Text, Title = "Antes", Body = "Contenido anterior" }, null);
            Call(window, "ProjectPrepared");
            Until(() => !Field<ProjectionSurface>(window, "_output").IsTransitioning, "Initial output did not settle");
            Call(window, "PrepareCue", cue, null); Call(window, "ProjectPrepared");
            Until(() => Field<PowerPointPresentation?>(window, "_powerPoint")?.IsRunning == true
                && !Field<ProjectionSurface>(window, "_output").IsTransitioning, "PowerPoint did not enter output");
            owner = Field<PowerPointPresentation>(window, "_powerPoint");
            var output = Field<ProjectionSurface>(window, "_output");
            Check(output.DisplayedCue is { Kind: CueKind.Image, FilePath: { } frame } && File.Exists(frame), "PowerPoint transitioned to a blank placeholder instead of its slide pixels");
            using var pixels = new System.Drawing.Bitmap(output.DisplayedCue!.FilePath!);
            Check(pixels.Width >= 1280 && pixels.GetPixel(pixels.Width / 2, pixels.Height / 2).R > 120,
                "Transition frame is black or a small thumbnail");
            Check(output.PresentedTransitions.Kind == "Slide" && output.PresentedTransitions.DurationMilliseconds == 900, "Active system transition was replaced");
        });
        Test("Changing scenes retains the native slideshow and its animation position", () =>
        {
            owner ??= Field<PowerPointPresentation>(window, "_powerPoint");
            owner.Advance(1); var build = owner.ClickIndex;
            var outputWindow = new ProjectionWindow(Field<ProjectionSurface>(window, "_output"), pauseOnClose: false);
            SetField(window, "_outputWindow", outputWindow); outputWindow.Show();
            Call(window, "RoutePowerPointOutput");
            Check(!outputWindow.IsVisible, "Native output did not cover its compositor window");
            Call(window, "PrepareCue", new Cue { Kind = CueKind.Text, Title = "Otra escena", Body = "Seguimos" }, null);
            Call(window, "ProjectPrepared");
            Until(() => !Field<ProjectionSurface>(window, "_output").IsTransitioning, "Replacement scene did not settle");
            Check(ReferenceEquals(owner, Field<PowerPointPresentation?>(window, "_powerPoint")) && owner.IsRunning, "Changing scenes closed the native slideshow");
            Check(outputWindow.IsVisible, "Leaving PowerPoint kept the congregation compositor window hidden");
            Check(owner.ClickIndex == build, "Changing scenes reset its animation build");
            var tile = workspace.SceneTiles.First(s => s.Action == "powerpoint" && s.Cue is null);
            var result = (Culto.Remote.CommandResult)Call(window, "ExecuteRemoteSceneMode", tile.Id, false)!;
            Check(result.Success, result.Message);
            Until(() => session.Live?.Kind == CueKind.PowerPoint && !Field<ProjectionSurface>(window, "_output").IsTransitioning, "PowerPoint scene did not return to output");
            Check(ReferenceEquals(owner, Field<PowerPointPresentation?>(window, "_powerPoint")) && owner.ClickIndex == build,
                "PowerPoint scene reopened or restarted the presentation");
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-handoff.png"));
        });
        Test("PowerPoint resumes through dissolve and intentional blackout without reopening", () =>
        {
            var native = Field<PowerPointPresentation>(window, "_powerPoint");
            workspace.Transitions = workspace.Transitions with { Kind = "Dissolve", DurationMilliseconds = 600 };
            Call(window, "ToggleBlackout");
            Until(() => !Field<ProjectionSurface>(window, "_output").IsTransitioning, "Blackout did not finish");
            Check(session.IsBlackout && native.IsRunning, "Blackout closed the native show");
            Call(window, "ToggleBlackout");
            Until(() => !Field<bool>(window, "_powerPointWaitingForTransition") && !Field<ProjectionSurface>(window, "_output").IsTransitioning,
                "Returning from blackout did not finish");
            Check(!session.IsBlackout && ReferenceEquals(native, Field<PowerPointPresentation>(window, "_powerPoint")), "Restoring reopened PowerPoint");
            Check(Field<ProjectionSurface>(window, "_output").PresentedTransitions.Kind == "Dissolve", "Restore ignored active dissolve");
        });
        Test("Restart during a retained PowerPoint transition overrides its old pause state", () =>
        {
            var native = Field<PowerPointPresentation>(window, "_powerPoint");
            native.SetPaused(true); SetField(window, "_isPaused", true);
            Call(window, "PrepareCue", new Cue { Kind = CueKind.Text, Title = "Pausa", Body = "Otra escena" }, null);
            Call(window, "ProjectPrepared");
            Until(() => !Field<ProjectionSurface>(window, "_output").IsTransitioning, "Paused replacement did not finish");
            var tile = workspace.SceneTiles.First(s => s.Action == "powerpoint" && s.Cue is null);
            Call(window, "ExecuteRemoteSceneMode", tile.Id, false);
            Call(window, "RestartMedia");
            Until(() => !Field<bool>(window, "_powerPointWaitingForTransition") && !Field<ProjectionSurface>(window, "_output").IsTransitioning,
                "Restart handoff did not finish");
            Check(!native.IsPaused && native.ClickIndex == 0, "Saved pause overrode the explicit restart intent");
        });
        Call(window, "CloseOutput");
    }
}
