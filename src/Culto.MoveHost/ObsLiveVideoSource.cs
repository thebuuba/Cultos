using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Culto.Core;

namespace Culto.MoveHost;

/// <summary>A synchronous libobs input; its render callback uploads the latest complete UI frame.</summary>
internal sealed class ObsLiveVideoSource : IDisposable
{
    internal const string Id = "culto_live_bgra";
    private static readonly nint NativeId = Marshal.StringToCoTaskMemUTF8(Id);
    private static readonly nint NativeName = Marshal.StringToCoTaskMemUTF8("Culto live BGRA");
    // These callbacks and strings live until process exit; libobs retains the function pointers.
    private static readonly ObsNative.SourceName NameCallback = _ => NativeName;
    private static readonly ObsNative.SourceCreate CreateCallback = Create;
    private static readonly ObsNative.SourceDestroy DestroyCallback = Destroy;
    private static readonly ObsNative.SourceDimension WidthCallback = data => data == 0 ? 0 : (uint)State(data)._width;
    private static readonly ObsNative.SourceDimension HeightCallback = data => data == 0 ? 0 : (uint)State(data)._height;
    private static readonly ObsNative.SourceRender RenderCallback = Render;
    private readonly SharedVideoFrame _frames;
    private readonly byte[] _pixels;
    private readonly int _width, _height;
    private readonly double _opacity;
    private nint _texture;
    internal static Exception? CallbackError;

    internal static void Register()
    {
        var info = new ObsNative.SourceInfo
        {
            Id = NativeId, Type = 0, OutputFlags = 1 | 128,
            GetName = Marshal.GetFunctionPointerForDelegate(NameCallback),
            Create = Marshal.GetFunctionPointerForDelegate(CreateCallback), Destroy = Marshal.GetFunctionPointerForDelegate(DestroyCallback),
            GetWidth = Marshal.GetFunctionPointerForDelegate(WidthCallback), GetHeight = Marshal.GetFunctionPointerForDelegate(HeightCallback),
            VideoRender = Marshal.GetFunctionPointerForDelegate(RenderCallback)
        };
        ObsNative.obs_register_source_s(ref info, (nuint)Marshal.SizeOf<ObsNative.SourceInfo>());
    }
    private ObsLiveVideoSource(string map, int width, int height, double opacity)
    {
        _width = width; _height = height; _opacity = opacity;
        _frames = SharedVideoFrame.Open(map, width, height);
        _pixels = new byte[_frames.ByteCount];
    }
    private static ObsLiveVideoSource State(nint data) => (ObsLiveVideoSource)GCHandle.FromIntPtr(data).Target!;
    private static nint Create(nint settings, nint source)
    {
        try
        {
            var state = new ObsLiveVideoSource(Marshal.PtrToStringUTF8(ObsNative.obs_data_get_string(settings, "map"))!,
                checked((int)ObsNative.obs_data_get_int(settings, "width")), checked((int)ObsNative.obs_data_get_int(settings, "height")),
                ObsNative.obs_data_get_double(settings, "opacity"));
            return GCHandle.ToIntPtr(GCHandle.Alloc(state));
        }
        catch (Exception error) { Volatile.Write(ref CallbackError, error); return 0; }
    }
    private static void Destroy(nint data)
    {
        if (data == 0) return;
        var handle = GCHandle.FromIntPtr(data);
        try { ((ObsLiveVideoSource)handle.Target!).Dispose(); }
        catch (Exception error) { Volatile.Write(ref CallbackError, error); }
        finally { handle.Free(); }
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Render(nint data, nint effect)
    {
        if (data == 0) return;
        try
        {
            var state = State(data);
            if (state._texture == 0)
            {
                state._texture = ObsNative.gs_texture_create((uint)state._width, (uint)state._height, 5 /* GS_BGRA */, 1, 0, 2 /* GS_DYNAMIC */);
                if (state._texture == 0) throw new InvalidOperationException("OBS could not allocate a live texture.");
                ObsNative.gs_texture_set_image(state._texture, state._pixels, (uint)state._width * 4, false);
            }
            if (state._frames.TryRead(state._pixels))
            {
                // WPF supplies straight BGRA. OBS scenes composite premultiplied alpha.
                for (int pixel = 0; pixel < state._pixels.Length; pixel += 4)
                {
                    int alpha = (int)(state._pixels[pixel + 3] * state._opacity + .5);
                    if (alpha == 255) continue;
                    state._pixels[pixel] = (byte)((state._pixels[pixel] * alpha + 127) / 255);
                    state._pixels[pixel + 1] = (byte)((state._pixels[pixel + 1] * alpha + 127) / 255);
                    state._pixels[pixel + 2] = (byte)((state._pixels[pixel + 2] * alpha + 127) / 255);
                    state._pixels[pixel + 3] = (byte)alpha;
                }
                ObsNative.gs_texture_set_image(state._texture, state._pixels, (uint)state._width * 4, false);
            }
            ObsNative.gs_effect_set_texture(ObsNative.gs_effect_get_param_by_name(effect, "image"), state._texture);
            ObsNative.gs_blend_state_push();
            try
            {
                ObsNative.gs_blend_function(1 /* ONE */, 5 /* INVSRCALPHA */);
                ObsNative.gs_draw_sprite(state._texture, 0, (uint)state._width, (uint)state._height);
            }
            finally { ObsNative.gs_blend_state_pop(); }
        }
        catch (Exception error) { Volatile.Write(ref CallbackError, error); }
    }
    public void Dispose()
    {
        ObsNative.obs_enter_graphics();
        try { if (_texture != 0) { ObsNative.gs_texture_destroy(_texture); _texture = 0; } }
        finally { ObsNative.obs_leave_graphics(); _frames.Dispose(); }
    }
}
