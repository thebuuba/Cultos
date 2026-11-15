using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Culto.Core;

namespace Culto.MoveHost;

internal sealed class ObsLiveMoveEngine(string runtimePath, int width, int height) : IDisposable
{
    private readonly ObsRuntime _runtime = new(runtimePath, width, height);

    public async Task RunAsync(ObsLiveMoveCommand command, ObsMoveProtocol protocol, CancellationToken token)
    {
        command.Validate();
        if (command.Width != width || command.Height != height) throw new InvalidDataException("Live canvas dimensions do not match worker dimensions.");
        ObsLiveVideoSource.CallbackError = null;
        using var sources = new LiveSources();
        // A watchdog reports a failed native renderer; it never substitutes animation timing.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(command.DurationMilliseconds + 15_000));
        token = deadline.Token;
        using var outgoing = CreateScene(command.Outgoing, "outgoing", sources);
        using var incoming = CreateScene(command.Incoming, "incoming", sources);
        var settings = ObsNative.obs_data_create();
        nint transition;
        try
        {
            var move = (command.Move ?? new()).Normalize();
            foreach (var (suffix, item) in new[] { ("in", move.In), ("out", move.Out) })
            {
                ObsNative.obs_data_set_int(settings, "position_" + suffix, item.Position);
                ObsNative.obs_data_set_double(settings, "zoom_" + suffix, item.Zoom);
                ObsNative.obs_data_set_double(settings, "curve_" + suffix, item.Curve);
                ObsNative.obs_data_set_int(settings, "easing_" + suffix, item.Easing);
                ObsNative.obs_data_set_int(settings, "easing_function_" + suffix, item.Function);
            }
            transition = ObsNative.obs_source_create_private("move_transition", "Culto Move", settings);
        }
        finally { ObsNative.obs_data_release(settings); }
        if (transition == 0) throw new InvalidOperationException("The real move_transition source is unavailable.");
        try
        {
            if (Marshal.PtrToStringUTF8(ObsNative.obs_source_get_id(transition)) != "move_transition")
                throw new InvalidOperationException("OBS did not create the requested Move plugin source.");
            ObsNative.obs_transition_set_size(transition, (uint)width, (uint)height);
            ObsNative.obs_transition_set(transition, ObsNative.obs_scene_get_source(outgoing.Scene));
            ObsNative.obs_set_output_source(0, transition);
            ulong outputAssignedAt = ObsNative.obs_get_video_frame_time();
            var timing = new ObsFrameTiming(Math.Max(outputAssignedAt, ObsNative.obs_get_video_frame_time()));
            using var frames = new ObsFramePump(transition, width, height, activateOutput: true);
            while (true)
            {
                ThrowCallbackError();
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
                ThrowCallbackError();
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

    private static void ThrowCallbackError()
    {
        if (Volatile.Read(ref ObsLiveVideoSource.CallbackError) is { } error)
            throw new InvalidOperationException("Native live video source failed.", error);
    }
    private static SceneSnapshot CreateScene(IReadOnlyList<ObsLiveLayer> layers, string role, LiveSources sources)
    {
        var scene = ObsNative.obs_scene_create_private("Culto live " + role);
        if (scene == 0) throw new InvalidOperationException("OBS could not create a live scene.");
        try
        {
            foreach (var layer in layers)
            {
                var source = sources.Get(layer);
                var item = ObsNative.obs_scene_add(scene, source);
                if (item == 0) throw new InvalidOperationException("OBS could not add a live scene layer.");
                var position = new ObsNative.Vec2 { X = (float)(layer.X + layer.Width / 2), Y = (float)(layer.Y + layer.Height / 2) };
                var scale = new ObsNative.Vec2 { X = (float)(layer.Width / layer.FrameWidth), Y = (float)(layer.Height / layer.FrameHeight) };
                ObsNative.obs_sceneitem_set_alignment(item, 0); // Center pivot matches WPF RotateTransform.
                ObsNative.obs_sceneitem_set_pos(item, ref position);
                ObsNative.obs_sceneitem_set_scale(item, ref scale);
                ObsNative.obs_sceneitem_set_rot(item, (float)layer.Rotation);
            }
            ThrowCallbackError();
            return new(scene);
        }
        catch { ObsNative.obs_scene_release(scene); throw; }
    }
    private sealed class LiveSources : IDisposable
    {
        private readonly Dictionary<(string Id, double Opacity), nint> _sources = [];
        public nint Get(ObsLiveLayer layer)
        {
            var key = (layer.SourceId, layer.Opacity);
            if (_sources.TryGetValue(key, out var source)) return source;
            var settings = ObsNative.obs_data_create();
            try
            {
                ObsNative.obs_data_set_string(settings, "map", layer.MapName);
                ObsNative.obs_data_set_int(settings, "width", layer.FrameWidth);
                ObsNative.obs_data_set_int(settings, "height", layer.FrameHeight);
                ObsNative.obs_data_set_double(settings, "opacity", layer.Opacity);
                // Same identity and opacity reuse one native source. Different opacity gets
                // a wrapper reading the same mailbox, retaining the name Move matches.
                source = ObsNative.obs_source_create_private(ObsLiveVideoSource.Id, layer.SourceId, settings);
                ThrowCallbackError();
                if (source == 0) throw new InvalidOperationException("OBS could not create a live layer source.");
                _sources.Add(key, source);
                return source;
            }
            catch { if (source != 0) ObsNative.obs_source_release(source); throw; }
            finally { ObsNative.obs_data_release(settings); }
        }
        public void Dispose() { foreach (var source in _sources.Values) ObsNative.obs_source_release(source); _sources.Clear(); }
    }
    private sealed record SceneSnapshot(nint Scene) : IDisposable
    { public void Dispose() => ObsNative.obs_scene_release(Scene); }
    public void Dispose() => _runtime.Dispose();
}
