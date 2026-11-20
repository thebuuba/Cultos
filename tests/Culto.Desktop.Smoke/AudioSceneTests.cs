using System.IO;
using System.Windows;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunAudioSceneTests(MainWindow window)
    {
        ((System.Windows.Controls.Slider)window.FindName("VolumeSlider")).Value = 0;
        Test("Starting an audio file keeps the scene in Program and its cover in Preview", () =>
        {
            var session = Field<PresentationSession>(window, "_session");
            var visual = new Cue { Kind = CueKind.Text, Title = "Escena actual", Body = "La imagen continúa" };
            Call(window, "PrepareCue", visual, null); Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            Until(() => !output.IsTransitioning, "Visual scene did not settle");
            var audio = new Cue { Kind = CueKind.Audio, Title = "Audio independiente", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
            Call(window, "PrepareCue", audio, null);
            Call(window, "PlayFileFromInput");
            Until(() => output.AudioCue?.Id == audio.Id && output.IsPlaying, "Audio file did not start");
            Check(session.Live?.Id == visual.Id && output.DisplayedCue?.Id == visual.Id, "Starting audio replaced the Program scene");
            Check(session.Prepared?.Id == audio.Id && ((FrameworkElement)window.FindName("PreparedMediaCover")).IsVisible,
                "Starting audio removed its prepared cover");
            Check(output.HasBackgroundMedia, "Audio controls do not target the independent audio");
            Call(window, "CloseOutput");
        });
        Test("Audio over a video scene replaces its sound without replacing its picture", () =>
        {
            var session = Field<PresentationSession>(window, "_session");
            var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv");
            var video = new Cue { Kind = CueKind.Video, Title = "Video en Programa", FilePath = path };
            Call(window, "PrepareCue", video, null); Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output"); output.Loop = true;
            Until(() => output.IsPlaying && !output.IsTransitioning, "Video scene did not start");
            var audio = new Cue { Kind = CueKind.Audio, Title = "Audio nuevo", FilePath = path };
            Call(window, "PrepareCue", audio, null); Call(window, "PlayFileFromInput");
            Until(() => output.AudioCue?.Id == audio.Id && output.IsPlaying, "Replacement audio did not start");
            Check(session.Live?.Id == video.Id && output.DisplayedCue?.Id == video.Id, "Audio replaced the video scene");
            var mixer = Field<object>(output, "_mixer");
            var foreground = Field<ProjectionSurface>(mixer, "_current");
            Check(Field<double>(foreground, "_mixGain") == 0, "Old video sound competes with the new audio");
            Check(ReferenceEquals(output.ActiveSurface, Field<ProjectionSurface>(mixer, "_background")), "Transport still targets the video instead of the selected audio");
            Call(window, "PrepareCue", new Cue { Kind = CueKind.Text, Title = "Texto nuevo", Body = "Conservar audio" }, null);
            Call(window, "ProjectPreparedPreservingOther");
            Until(() => !output.IsTransitioning, "Preserving scene change did not finish");
            Check(output.AudioCue?.Id == audio.Id, "Visual transition brought back the original video audio");
            Call(window, "CloseOutput");
        });
    }
}
