using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Culto.Core;

namespace Culto.MoveHost;

internal sealed class ObsMoveEngine(string runtimePath, int width, int height) : IDisposable
{
    private readonly ObsRuntime _runtime = new(runtimePath, width, height);

    public async Task RunAsync(ObsMoveCommand command, ObsMoveProtocol protocol, CancellationToken token)
    {
        ValidatePng(command.OutgoingPath); ValidatePng(command.IncomingPath);
        // A watchdog reports a failed native renderer; it never substitutes animation timing.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(command.DurationMilliseconds + 15_000));
        token = deadline.Token;
        using var outgoing = CreateScene(command.OutgoingPath, "outgoing");
        using var incoming = CreateScene(command.IncomingPath, "incoming");
        var transition = ObsNative.obs_source_create_private("move_transition", "Culto Move", 0);
        if (transition == 0) throw new InvalidOperationException("The real move_transition source is unavailable.");
        try
        {
            if (Marshal.PtrToStringUTF8(ObsNative.obs_source_get_id(transition)) != "move_transition")
                throw new InvalidOperationException("OBS did not create the requested Move plugin source.");
            ObsNative.obs_transition_set_size(transition, (uint)width, (uint)height);
            ObsNative.obs_transition_set(transition, ObsNative.obs_scene_get_source(outgoing.Scene));
            ObsNative.obs_set_output_source(0, transition);
            ulong outputAssignedAt = ObsNative.obs_get_video_frame_time();
            while (ObsNative.obs_source_get_width(outgoing.Image) == 0 || ObsNative.obs_source_get_height(outgoing.Image) == 0
                || ObsNative.obs_source_get_width(incoming.Image) == 0 || ObsNative.obs_source_get_height(incoming.Image) == 0)
                await Task.Delay(15, token).ConfigureAwait(false);
            var timing = new ObsFrameTiming(Math.Max(outputAssignedAt, ObsNative.obs_get_video_frame_time()));
            using var frames = new ObsFramePump(transition, width, height);
            while (true)
            {
                using var initial = await frames.ReadAsync(token).ConfigureAwait(false);
                // A new callback can carry a frame buffered before this command or source assignment.
                if (!timing.TryAcceptInitialFrame(initial.Timestamp)) continue;
                await protocol.WriteAsync(ObsMoveMessageType.Frame, initial.Buffer.AsMemory(0, initial.Length), token).ConfigureAwait(false);
                break;
            }
            if (!ObsNative.obs_transition_start(transition, 0, (uint)command.DurationMilliseconds, ObsNative.obs_scene_get_source(incoming.Scene)))
                throw new InvalidOperationException("The Move plugin refused to start the transition.");
            timing.TransitionStarted(ObsNative.obs_get_video_frame_time());
            ulong completedRenderTime = 0;
            while (true)
            {
                using var frame = await frames.ReadAsync(token).ConfigureAwait(false);
                if (!timing.IsTransitionFrame(frame.Timestamp)) continue; // Classify render time, not callback arrival.
                if (!float.IsFinite(frame.Progress)) throw new InvalidDataException("OBS returned invalid transition progress.");
                if (frame.Progress >= 1 && completedRenderTime == 0)
                    completedRenderTime = ObsNative.obs_get_video_frame_time();
                await protocol.WriteAsync(ObsMoveMessageType.Frame, frame.Buffer.AsMemory(0, frame.Length), token).ConfigureAwait(false);
                // Wait for a real native frame rendered after OBS reported completion,
                // so the last delivered frame contains the destination, not buffered motion.
                if (completedRenderTime != 0 && frame.Progress >= 1 && frame.Timestamp > completedRenderTime) break;
            }
            // Disconnect before releasing transition/scenes; Complete follows the final frame.
        }
        finally
        {
            ObsNative.obs_set_output_source(0, 0);
            ObsNative.obs_transition_set(transition, 0);
            ObsNative.obs_source_release(transition);
        }
        await protocol.WriteAsync(ObsMoveMessageType.Complete, ReadOnlyMemory<byte>.Empty, token).ConfigureAwait(false);
    }

    private SceneSnapshot CreateScene(string path, string role)
    {
        var scene = ObsNative.obs_scene_create_private("Culto " + role);
        if (scene == 0) throw new InvalidOperationException("OBS could not create a private scene.");
        var settings = ObsNative.obs_data_create();
        nint image = 0;
        try
        {
            if (settings == 0) throw new InvalidOperationException("OBS could not create source settings.");
            ObsNative.obs_data_set_string(settings, "file", path);
            ObsNative.obs_data_set_bool(settings, "unload", false);
            // Distinct names make these unmatched full-frame items use Move's actual in/out defaults.
            image = ObsNative.obs_source_create_private("image_source", "Culto snapshot " + role, settings);
            if (image == 0) throw new InvalidOperationException("The OBS image_source plugin is unavailable.");
            var item = ObsNative.obs_scene_add(scene, image);
            if (item == 0) throw new InvalidOperationException("OBS could not add the snapshot to its private scene.");
            var bounds = new ObsNative.Vec2 { X = width, Y = height };
            ObsNative.obs_sceneitem_set_alignment(item, 1 | 4); // left/top
            ObsNative.obs_sceneitem_set_bounds_type(item, 1); // OBS_BOUNDS_STRETCH
            ObsNative.obs_sceneitem_set_bounds(item, ref bounds);
            return new SceneSnapshot(scene, image);
        }
        catch { ObsNative.obs_scene_release(scene); if (image != 0) ObsNative.obs_source_release(image); throw; }
        finally { if (settings != 0) ObsNative.obs_data_release(settings); }
    }

    private static void ValidatePng(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("Snapshot paths must be absolute.");
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        stream.ReadExactly(header);
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!header[..8].SequenceEqual(signature) || !header.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Move snapshots must be PNG images.");
        ObsMoveProtocol.FrameByteCount(BinaryPrimitives.ReadInt32BigEndian(header[16..20]), BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
    }

    private sealed record SceneSnapshot(nint Scene, nint Image) : IDisposable
    {
        public void Dispose() { ObsNative.obs_scene_release(Scene); ObsNative.obs_source_release(Image); }
    }
    public void Dispose() => _runtime.Dispose();
}
