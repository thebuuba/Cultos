using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop.Projection;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    static async Task RunLiveMediaMoveChecks()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CultoLiveMedia-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var videoPath = Path.Combine(directory, "long.mkv");
        var start = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-stream_loop", "19", "-i", Path.Combine(Folder, "test.mkv"), "-c", "copy", "-t", "60", videoPath }) start.ArgumentList.Add(argument);
        using (var fixture = Process.Start(start)!)
        {
            var message = await fixture.StandardError.ReadToEndAsync(); await fixture.WaitForExitAsync();
            Check(fixture.ExitCode == 0, "Long live-video fixture is available " + message);
        }
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 0, Loop = true,
            Transitions = new TransitionOptions { Kind = "Move", DurationMilliseconds = 1500, AudioFadeInMilliseconds = 300, AudioFadeOutMilliseconds = 300 } };
        var errors = new List<string>(); output.PlaybackError += errors.Add;
        var window = new Window { Content = output, Width = 656, Height = 400, Topmost = true }; window.Show();
        async Task Until(Func<bool> ready, string failure, int timeout = 45000)
        {
            var end = DateTime.UtcNow.AddMilliseconds(timeout);
            while (!ready() && errors.Count == 0 && DateTime.UtcNow < end) await Task.Delay(20);
            var mixer = typeof(ProjectionSurface).GetField("_mixer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(output)!;
            var pending = (ProjectionSurface?)mixer.GetType().GetField("_incoming", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(mixer);
            Check(ready() && errors.Count == 0, failure + " " + string.Join(" | ", errors)
                + $" playing={output.IsPlaying} paused={output.IsPaused} pos={output.Position} transition={output.IsTransitioning} engine={output.IsPreparingPlaybackEngine}"
                + " layers=" + string.Join(";", (pending ?? output.ActiveSurface).GetLiveLayers().Select(l => $"{l.SourceId}:play={l.Surface.IsPlaying},pause={l.Surface.IsPaused},time={l.Surface.Position},frame={l.Surface.HasVideoFrame}")));
        }
        bool NativeVisible() => Descendants(output).OfType<Image>().Any(image => image.Visibility == Visibility.Visible && Panel.GetZIndex(image) == 1000 && image.Source is not null);
        try
        {
            var source = new Cue { Kind = CueKind.Video, FilePath = videoPath, Title = "Video vivo" };
            var first = new Cue { Kind = CueKind.Scene, Title = "Video A", Scene = new SceneComposition { Width = 320, Height = 180,
                Layers = [new SceneLayer { SourceId = "shared-video", Source = source, X = 0, Y = 50, Width = 140, Height = 80, Volume = 50 }] } };
            var second = first.Copy(newId: true); second.Title = "Video B"; second.Scene!.Layers[0].X = 170;
            output.ShowCue(first);
            await Until(() => output.IsPlaying && output.Position > .5 && !output.IsTransitioning, "Layered video starts with an advancing media clock");
            var shared = output.ActiveSurface.GetLiveLayers().Single().Surface;
            var player = (MediaPlayer)typeof(ProjectionSurface).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shared)!;
            var video = Descendants(shared).OfType<CompositedVideo>().Single();
            int frames = 0; video.FramePresented += () => frames++;
            output.Volume = 20;
            await Until(() => shared.Volume == 10 && player.Volume == 10, "Layer gain scales a quiet master volume");
            var position = player.Time;
            var initialFrames = frames;
            output.TransitionTo(second);
            await Until(NativeVisible, "Shared layered video enters actual native Move");
            Check(ReferenceEquals(shared, output.ActiveSurface.GetLiveLayers().Single().Surface), "Shared layered video keeps exactly the original decoder during native Move");
            bool gainStable = true, clockStable = true;
            var until = DateTime.UtcNow.AddSeconds(15);
            while (output.IsTransitioning && DateTime.UtcNow < until && errors.Count == 0)
            {
                gainStable &= shared.Volume == 10 && player.Volume == 10;
                clockStable &= player.IsPlaying && player.Time >= position - 100;
                await Task.Delay(20);
            }
            Check(errors.Count == 0 && !output.IsTransitioning && output.PresentedTransitions.Kind == "Move", "Layered video completes real native Move without fallback " + string.Join(" | ", errors));
            Check(gainStable, "Shared video audio gain stays constant throughout geometry-only Move");
            Check(clockStable && player.Time > position + 600 && frames - initialFrames > 8, "Native Move keeps the original video clock and decoded frame events advancing");
            Check(ReferenceEquals(shared, output.ActiveSurface.GetLiveLayers().Single().Surface), "Completed layered Move preserves the shared decoder");

            position = player.Time; initialFrames = frames;
            var text = new Cue { Kind = CueKind.Text, Body = "Texto con audio de las capas" };
            output.TransitionVisual(text);
            await Until(NativeVisible, "Layered-video to text uses actual native Move");
            gainStable = true;
            until = DateTime.UtcNow.AddSeconds(15);
            while (output.IsTransitioning && DateTime.UtcNow < until && errors.Count == 0)
            {
                gainStable &= shared.Volume == 10 && player.Volume == 10 && player.IsPlaying;
                await Task.Delay(20);
            }
            Check(errors.Count == 0 && !output.IsTransitioning && output.HasBackgroundMedia && gainStable, "Text foreground preserves the layered video's live audio gain");
            Check(player.Time > position + 600 && frames - initialFrames > 8, "Retained layered video continues decoding behind text");
            position = player.Time;
            output.TransitionTo(second);
            await Until(() => !output.IsTransitioning && !output.HasBackgroundMedia && output.DisplayedCue?.Id == second.Id, "Layered scene returns from retained background");
            Check(ReferenceEquals(shared, output.ActiveSurface.GetLiveLayers().Single().Surface) && player.Time > position && player.IsPlaying, "Restoring the layered scene does not restart its shared video");
            Check(shared.Volume == 10 && player.Volume == 10, "Restored layered video retains the requested source gain");

            output.Volume = 0;
            output.TransitionTo(first);
            await Until(NativeVisible, "Another layered native Move starts before interruption");
            var cut = new Cue { Kind = CueKind.Text, Body = "Corte inmediato" };
            output.Transitions = new TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
            output.TransitionTo(cut);
            await Until(() => !output.IsTransitioning && output.DisplayedCue?.Id == cut.Id, "Cut interrupts the running live native Move");
            Check(!NativeVisible(), "Cut removes the cancelled native overlay");
        }
        finally { output.Dispose(); window.Close(); }

        using var client = new ObsLiveMoveClient();
        var missing = new ObsLiveLayer("missing", "Local\\CultoLive-" + Guid.NewGuid().ToString("N"), 320, 180, 0, 0, 320, 180);
        bool rejected = false;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            try { await client.RenderAsync(new ObsLiveMoveCommand(320, 180, 500, new MoveOptions(), [missing], [missing]), _ => { }, timeout.Token); }
            catch (Exception error) when (error is InvalidOperationException or IOException) { rejected = true; }
        }
        Check(rejected, "Native live client reports a missing source map without hanging");
        using var map = SharedVideoFrame.Create(320, 180);
        var pixels = new byte[320 * 180 * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i + 2] = 255; pixels[i + 3] = 255; }
        map.Write(pixels);
        var layer = new ObsLiveLayer("recovery", map.MapName, 320, 180, 0, 0, 320, 180);
        int renderedFrames = 0;
        await client.RenderAsync(new ObsLiveMoveCommand(320, 180, 300, new MoveOptions(), [layer], [layer with { X = 20 }]), _ => renderedFrames++, CancellationToken.None);
        Check(renderedFrames > 1, "Native live client restarts successfully after a source failure");
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        bool cancelled = false;
        var clock = Stopwatch.StartNew();
        try
        {
            await client.RenderAsync(new ObsLiveMoveCommand(320, 180, 5000, new MoveOptions(), [layer], [layer with { X = 100 }]), _ => cancel.Cancel(), cancel.Token);
        }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && clock.ElapsedMilliseconds < 5000, "Native live client interrupts a running transition promptly");
        Directory.Delete(directory, recursive: true);
    }
}
