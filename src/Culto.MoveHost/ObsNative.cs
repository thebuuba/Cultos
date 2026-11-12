using System.Runtime.InteropServices;

namespace Culto.MoveHost;

// OBS 33.0.1 C ABI; bool is a one-byte C bool. This host is x64 only.
internal static class ObsNative
{
    internal const string Library = "culto-libobs";
    [StructLayout(LayoutKind.Sequential)] internal struct VideoInfo
    {
        public nint GraphicsModule;
        public uint FpsNumerator, FpsDenominator, BaseWidth, BaseHeight, OutputWidth, OutputHeight;
        public int OutputFormat;
        public uint Adapter;
        [MarshalAs(UnmanagedType.I1)] public bool GpuConversion;
        public int ColorSpace, Range, ScaleType;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct AudioInfo { public uint SamplesPerSecond; public int Speakers; }
    [StructLayout(LayoutKind.Sequential)] internal struct Vec2 { public float X, Y; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void VideoCallback(nint parameter, nint frame);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void RenderCallback(nint parameter, uint width, uint height);
    [StructLayout(LayoutKind.Sequential)] internal struct GraphicsRect { public int X, Y, Width, Height; }
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool obs_startup([MarshalAs(UnmanagedType.LPUTF8Str)] string locale, [MarshalAs(UnmanagedType.LPUTF8Str)] string configPath, nint profiler);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_shutdown();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_add_data_path([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int obs_reset_video(ref VideoInfo info);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool obs_get_video_info(out VideoInfo info);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool obs_reset_audio(ref AudioInfo info);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int obs_open_module(out nint module, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, [MarshalAs(UnmanagedType.LPUTF8Str)] string dataPath);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool obs_init_module(nint module);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_post_load_modules();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint obs_data_create();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_data_release(nint data);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_data_set_string(nint data, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_data_set_bool(nint data, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [MarshalAs(UnmanagedType.I1)] bool value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint obs_source_create_private([MarshalAs(UnmanagedType.LPUTF8Str)] string id, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nint settings);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_source_release(nint source);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern uint obs_source_get_width(nint source);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern uint obs_source_get_height(nint source);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint obs_source_get_id(nint source);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint obs_scene_create_private([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_scene_release(nint scene);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint obs_scene_get_source(nint scene);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint obs_scene_add(nint scene, nint source);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_sceneitem_set_bounds_type(nint item, int type);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_sceneitem_set_alignment(nint item, uint alignment);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_sceneitem_set_bounds(nint item, ref Vec2 bounds);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_transition_set_size(nint transition, uint width, uint height);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_transition_set(nint transition, nint source);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool obs_transition_start(nint transition, int mode, uint durationMilliseconds, nint destination);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern float obs_transition_get_time(nint transition);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_set_output_source(uint channel, nint source);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint obs_get_video();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern ulong obs_get_video_frame_time();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool video_output_connect(nint video, nint conversion, VideoCallback callback, nint parameter);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void video_output_disconnect(nint video, VideoCallback callback, nint parameter);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_add_raw_video_callback(nint conversion, VideoCallback callback, nint parameter);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_remove_raw_video_callback(VideoCallback callback, nint parameter);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_add_main_render_callback(RenderCallback callback, nint parameter);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void obs_remove_main_render_callback(RenderCallback callback, nint parameter);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void gs_matrix_push();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void gs_matrix_pop();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void gs_matrix_translate3f(float x, float y, float z);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void gs_matrix_scale3f(float x, float y, float z);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void gs_set_scissor_rect(ref GraphicsRect rect);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void gs_set_scissor_rect(nint rect);
}
