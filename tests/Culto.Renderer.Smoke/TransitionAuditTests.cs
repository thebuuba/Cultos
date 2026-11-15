using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    static async Task RunTransitionAuditChecks()
    {
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 0, Loop = true };
        var window = new Window { Content = output, Width = 640, Height = 400, Topmost = true }; window.Show();
        async Task Until(Func<bool> ready, string failure, int timeout = 10000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
            while (!ready() && DateTime.UtcNow < deadline) await Task.Delay(15);
            Check(ready(), failure);
        }
        var video = new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv"), Title = "Audit video" };
        try
        {
            output.ShowCue(video);
            await Until(() => output.IsPlaying && !output.IsTransitioning, "Audit video starts", 30000);
            output.Transitions = new TransitionOptions { Kind = "Move", DurationMilliseconds = 500, AudioFadeOutMilliseconds = 0, AudioFadeInMilliseconds = 0 };
            var visual = new Cue { Kind = CueKind.Text, Body = "Audio conservado" };
            output.TransitionVisual(visual);
            Check(output.PresentedTransitions.Kind == "Move", "Preserving audio uses real native Move without substituting dissolve");
            await Until(() => !output.IsTransitioning, "Preserved-audio visual transition finishes");
            Check(output.HasBackgroundMedia && output.AudioCue?.Id == video.Id, "Preserved-audio fallback keeps the live audio source");
            output.ShowCue(video); output.SetPaused(false);
            await Until(() => output.IsPlaying && !output.IsTransitioning, "Audit video restarts");
            output.Transitions = new TransitionOptions { Kind = "Dissolve", DurationMilliseconds = 500, AudioFadeOutMilliseconds = 5000 };
            output.TransitionTo(new Cue { Kind = CueKind.Text, Body = "Fundido largo" });
            await Until(() => output.DisplayedCue?.Kind == CueKind.Text && output.IsTransitioning, "Long audio envelope is active");
            output.Transitions = new TransitionOptions { Kind = "Cut", AudioFadeOutMilliseconds = 0, AudioFadeInMilliseconds = 0 };
            output.TransitionToBackdrop(output.DisplayedCue, true, false);
            await Until(() => output.DisplayedBlackout, "Cut-to-black interrupts a long running audio fade immediately", 250);
            Check(!output.IsTransitioning, "Emergency cut leaves no old request queued");
        }
        finally { window.Close(); }
    }
}
