using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    static async Task RunGlobalTransitionChecks(ProjectionSurface output, ProjectionSurface preview, Cue video)
    {
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        object? Field(string name) => typeof(ProjectionSurface).GetField(name, fields)!.GetValue(output);
        MediaPlayer Player() => (MediaPlayer)Field("_player")!;
        var root = (Grid)Field("_root")!;
        var outgoing = (Image)Field("_dissolve")!;
        bool Current(Cue cue) => ((string?)Field("_mediaKey"))?.StartsWith(cue.Id + "|", StringComparison.Ordinal) == true;
        bool Clean() => !output.IsTransitioning && outgoing.Source is null
            && outgoing.Visibility == Visibility.Collapsed && root.RenderTransform.Value.IsIdentity
            && outgoing.RenderTransform.Value.IsIdentity && root.RenderTransformOrigin == new Point()
            && outgoing.RenderTransformOrigin == new Point();
        async Task Until(Func<bool> condition, string failure)
        {
            var deadline = DateTime.UtcNow.AddSeconds(8);
            var states = new List<string>();
            while (!condition() && DateTime.UtcNow < deadline)
            {
                if (Field("_player") is MediaPlayer player)
                {
                    var state = $"state={player.State},volume={player.Volume},mute={player.Mute},gain={Field("_audioGain")},awaiting={Field("_awaitingAudioStart")},paused={output.IsPaused}";
                    if (states.LastOrDefault() != state) states.Add(state);
                }
                await Task.Delay(10);
            }
            if (!condition()) throw new Exception("FAIL: " + failure + ": " + string.Join("; ", states.TakeLast(20)));
        }
        byte[] Center()
        {
            output.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)output.ActualWidth, (int)output.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(output);
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
            return pixel;
        }
        var directory = Path.Combine(Path.GetTempPath(), "CultoGlobalTransitions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Cue Solid(string name, byte red, byte blue)
        {
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { blue, 0, red, 255 }, 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(directory, name + ".png");
            using var stream = File.Create(path); encoder.Save(stream);
            return new Cue { Kind = CueKind.Image, FilePath = path, Title = name };
        }
        var red = Solid("red", 255, 0); var blue = Solid("blue", 0, 255);
        var text = new Cue { Kind = CueKind.Text, Body = "Global transition text" };
        var savedOptions = output.Transitions; var savedStyle = output.BibleStyle;
        var savedPreviewOptions = preview.Transitions; var savedBackground = output.BackgroundImagePath;
        var options = new TransitionOptions { Kind = "Dissolve", DurationMilliseconds = 300,
            AudioFadeOutMilliseconds = 600, AudioFadeInMilliseconds = 180 };
        async Task<Cue> StartVideo()
        {
            output.SetPaused(true); output.ShowCue(null); output.Volume = 75;
            var clip = new Cue { Kind = CueKind.Video, FilePath = video.FilePath, Title = "Global fade fixture" };
            output.ShowCue(clip);
            await Until(() => Current(clip) && output.IsPlaying && Player().Volume == 75,
                "Global transition video did not reach its requested native volume");
            return clip;
        }
        try
        {
            output.SetPaused(true); output.ShowCue(red); output.Loop = false;
            output.BibleStyle = new BibleVisualStyle();
            foreach (var kind in new[] { "Slide", "Zoom" })
            {
                output.Transitions = options with { Kind = kind, DurationMilliseconds = 500 };
                output.ShowCue(red); output.TransitionTo(blue);
                Check(output.IsTransitioning && (kind == "Slide" ? root.RenderTransform is TranslateTransform : root.RenderTransform is ScaleTransform),
                    kind + " animates the incoming frame with its configured transform");
                await Until(() => kind == "Slide"
                    ? root.RenderTransform is TranslateTransform translation && translation.X > 0 && translation.X < output.ActualWidth
                    : root.RenderTransform is ScaleTransform scale && scale.ScaleX > .88 && scale.ScaleX < 1 && scale.ScaleY > .88 && scale.ScaleY < 1,
                    kind + " never reached an intermediate animated position or scale");
                Check(output.IsTransitioning, kind + " reaches an intermediate animated position or scale before completing");
                await Until(() => !output.IsTransitioning, kind + " did not complete");
                Check(Clean() && Center()[0] > 240 && Center()[2] < 10,
                    kind + " finishes on the incoming image and releases snapshots and transforms");
                output.TransitionTo(red); await Task.Delay(80);
                output.ShowCue(red, blackout: true);
                Check(Clean() && Center().Take(3).All(value => value == 0), kind + " blackout cancels transforms and outgoing frame immediately");
                await Task.Delay(600);
                Check(Clean() && Center().Take(3).All(value => value == 0), kind + " cancelled completion cannot reveal stale content");
                output.ShowCue(red); output.TransitionTo(blue); await Task.Delay(80); output.TransitionTo(red);
                await Until(() => !output.IsTransitioning, kind + " replacement did not finish");
                Check(Clean() && Center()[2] > 240 && Center()[0] < 10, kind + " interrupted visual transition finishes on the latest image");
            }

            output.Transitions = options with { Kind = "Cut" };
            output.ShowCue(red); output.TransitionTo(blue);
            Check(Clean() && Center()[0] > 240, "Cut immediately replaces static content without retaining an outgoing frame");

            // Both visual kinds retain the old decoder during the independently configured audio fade.
            foreach (var destination in new[] { "image", "text", "black", "background" })
            {
                output.Transitions = options with { Kind = destination == "text" ? "Cut" : "Dissolve" };
                var clip = await StartVideo();
                var media = Field("_media"); var volumes = new List<int>();
                var timer = Stopwatch.StartNew();
                output.BackgroundImagePath = blue.FilePath;
                if (destination == "image") output.TransitionTo(red);
                else if (destination == "text") output.TransitionTo(text);
                else output.TransitionToBackdrop(clip, blackout: destination == "black", hideText: destination == "background");
                Check(Current(clip) && output.IsPlaying, "Video to " + destination + " retains native playback for the outgoing fade");
                await Until(() =>
                {
                    volumes.Add(Player().Volume);
                    return destination is "image" or "text" ? !Current(clip) : !output.IsPlaying;
                }, "Video to " + destination + " did not finish its outgoing audio fade");
                Check(volumes.Any(value => value > 0 && value < 75) && timer.ElapsedMilliseconds >= 450,
                    "Video to " + destination + " uses the configured 600 ms native audio fade independently of the visual duration");
                await Until(() => !output.IsTransitioning, "Video to " + destination + " did not finish its visual transition");
                Check(!output.IsPlaying && (Player().Volume <= 0 || (double)Field("_audioGain")! == 0),
                    "Video to " + destination + " ends with silent native playback");
                if (destination is "black" or "background")
                    Check(output.IsPaused && ReferenceEquals(media, Field("_media"))
                        && (destination == "black" ? Center().Take(3).All(value => value == 0) : Center()[0] > 240),
                        "Video to " + destination + " preserves the paused decoder and reaches the requested backdrop");
                else if (destination == "image") Check(Center()[2] > 240, "Video to image reaches the requested output pixels");
                else Check(Descendants(output).OfType<TextBlock>().Any(block => block.Text == text.Body && block.IsVisible),
                    "Video to text reaches the requested visible content with Cut");
            }

            output.Transitions = options;
            var old = await StartVideo();
            output.TransitionTo(red); await Task.Delay(70); output.TransitionTo(text);
            await Until(() => !Current(old) && !output.IsTransitioning, "Latest request did not replace a pending video fade");
            await Task.Delay(700);
            Check(Clean() && !output.IsPlaying && Descendants(output).OfType<TextBlock>().Any(block => block.Text == text.Body && block.IsVisible),
                "Cancelled outgoing-audio callbacks cannot replace the latest requested text later");

            output.Transitions = options;
            old = await StartVideo();
            var snapshot = options with { Kind = "Slide", DurationMilliseconds = 650, AudioFadeInMilliseconds = 700 };
            output.Transitions = snapshot;
            snapshot.Kind = "Cut"; snapshot.AudioFadeInMilliseconds = 0;
            var getterCopy = output.Transitions; getterCopy.Kind = "Zoom";
            Check(output.Transitions.Kind == "Slide" && output.Transitions.AudioFadeInMilliseconds == 700,
                "Renderer settings setter and getter both isolate mutable options");
            var incoming = new Cue { Kind = CueKind.Video, FilePath = video.FilePath, Title = "Snapshot incoming video" };
            output.TransitionTo(incoming);
            output.Transitions = options with { Kind = "Cut", DurationMilliseconds = 100, AudioFadeOutMilliseconds = 0, AudioFadeInMilliseconds = 0 };
            await Until(() => Current(incoming) && root.RenderTransform is TranslateTransform,
                "Queued presentation lost its snapshotted Slide setting");
            var applied = (TransitionOptions)Field("_presentedTransitions")!;
            Check(applied.Kind == "Slide" && applied.DurationMilliseconds == 650 && applied.AudioFadeOutMilliseconds == 600
                && applied.AudioFadeInMilliseconds == 700 && (int)Field("_dissolveMilliseconds")! == 650,
                "Queued video retains all transition settings captured before the settings edit");
            var incomingVolumes = new List<int>();
            await Until(() => { incomingVolumes.Add(Player().Volume); return Player().Volume == 75 && !output.IsTransitioning; },
                "Snapshotted incoming video fade did not settle");
            Check(incomingVolumes.Any(value => value > 0 && value < 75),
                "Incoming native audio still fades after a later settings change disables fades");
            output.SetPaused(true); output.ShowCue(red); output.TransitionTo(blue);
            Check(Clean() && Center()[0] > 240, "The next presentation uses newly edited Cut settings");

            await StartVideo();
            incoming = new Cue { Kind = CueKind.Video, FilePath = video.FilePath, Title = "Zero fade incoming video" };
            output.TransitionTo(incoming);
            Check(Current(incoming) && !output.IsTransitioning,
                "Zero outgoing audio fade with Cut commits the next media immediately");
            await Until(() => output.IsPlaying && Player().Volume == 75, "Zero incoming audio fade did not restore the native volume");
            Check((double)Field("_audioGain")! == 1, "Zero incoming fade reaches full native gain without leaving a pending envelope");
            var audio = new Cue { Kind = CueKind.Audio, FilePath = Path.Combine(Folder, "audio.wav"), Title = "Zero fade audio-only cue" };
            output.TransitionTo(audio);
            await Until(() => Current(audio) && output.IsPlaying && Player().Volume == 75 && !Player().Mute,
                "Zero fade audio-only cue did not reach the requested unmuted native volume");
            Check(!output.IsTransitioning, "Zero fade audio-only playback starts without requiring a video frame or visual animation");
            output.SetPaused(true); output.ShowCue(red);

            var verse = new Cue { Kind = CueKind.Bible, Body = "Bible transition inheritance", Reference = "Test 1:1" };
            output.Transitions = options with { Kind = "Slide" };
            output.BibleStyle = new BibleVisualStyle();
            output.TransitionTo(verse);
            Check(root.RenderTransform is TranslateTransform && output.IsTransitioning, "Bible System setting inherits the global Slide transition");
            await Until(() => !output.IsTransitioning, "Inherited Bible transition did not complete");
            output.BibleStyle = new BibleVisualStyle { Transition = "Cut" };
            output.ShowCue(red); output.TransitionTo(verse);
            Check(Clean(), "Bible Cut override bypasses the global Slide transition");
            output.BibleStyle = new BibleVisualStyle { Transition = "Dissolve", TransitionMilliseconds = 550 };
            output.ShowCue(red); output.TransitionTo(verse);
            Check(output.IsTransitioning && root.RenderTransform.Value.IsIdentity && (int)Field("_dissolveMilliseconds")! == 550,
                "Bible Dissolve override retains its own visual kind and duration");
            await Until(() => !output.IsTransitioning, "Overridden Bible transition did not complete");

            preview.Transitions = options with { Kind = "Zoom", AudioFadeInMilliseconds = 0 };
            preview.Volume = 100; preview.ShowCue(video); preview.SetPaused(false);
            await Until(() => preview.IsPlaying, "Configured preview did not start");
            var previewPlayer = (MediaPlayer)typeof(ProjectionSurface).GetField("_player", fields)!.GetValue(preview)!;
            Check(!preview.IsTransitioning && preview.Volume == 0 && previewPlayer.Volume <= 0
                && (previewPlayer.Mute || previewPlayer.AudioTrack == -1),
                "Global transitions and zero audio fade cannot unmute preview playback");
        }
        finally
        {
            output.SetPaused(true); preview.SetPaused(true); output.ShowCue(null);
            output.Transitions = savedOptions; output.BibleStyle = savedStyle;
            output.BackgroundImagePath = savedBackground; preview.Transitions = savedPreviewOptions;
            File.Delete(red.FilePath!); File.Delete(blue.FilePath!); Directory.Delete(directory);
        }
    }
}
