using System.Runtime.CompilerServices;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static int RunVideoSceneSelectionTests()
    {
        // Exercise the selection policy without creating a window or native player.
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        var session = new PresentationSession();
        var remembered = new Cue { Kind = CueKind.Video, Title = "Old video" };
        var workspace = new WorkspaceData { QuickSceneVideo = remembered };
        SetField(window, "_workspace", workspace);
        SetField(window, "_session", session);
        Test("Video scene ignores a remembered video when no clip is prepared or active", () =>
        {
            session.Prepare(new Cue { Kind = CueKind.Text });
            Check(Call(window, "SceneMedia", CueKind.Video) is null, "Scene selected a remembered video");
        });
        Test("Video scene uses the prepared video", () =>
        {
            var prepared = new Cue { Kind = CueKind.Video, Title = "Prepared video" };
            session.Prepare(prepared);
            Check(ReferenceEquals(prepared, Call(window, "SceneMedia", CueKind.Video)), "Prepared video was not selected");
        });
        Test("Video scene does not remember a newly used video", () =>
        {
            var next = new Cue { Kind = CueKind.Video };
            Check(Call(window, "RememberSceneMedia", next) is true, "Video could not be used");
            Check(ReferenceEquals(remembered, workspace.QuickSceneVideo), "Video updated the persistent shortcut");
        });
        return failures == 0 ? 0 : 1;
    }
}
