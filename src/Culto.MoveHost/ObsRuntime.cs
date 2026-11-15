using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Culto.MoveHost;

internal sealed class ObsRuntime : IDisposable
{
    private readonly List<nint> _searchDirectories = [];
    private bool _started;
    public string Root { get; }
    public ObsRuntime(string root, int width, int height, bool captureOnly = false)
    {
        Root = Path.GetFullPath(root);
        try
        {
            if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("OBS Move requires an x64 worker.");
            // Only registered runtime directories and System32 may satisfy native dependencies.
            if (!SetDefaultDllDirectories(0x00000800 | 0x00000400)) throw new Win32Exception(Marshal.GetLastWin32Error());
            foreach (var path in new[] { Path.Combine(Root, "bin", "64bit"), Path.Combine(Root, "obs-plugins", "64bit") })
            {
                if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
                var cookie = AddDllDirectory(path);
                if (cookie == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                _searchDirectories.Add(cookie);
            }
            var library = NativeLibrary.Load(RequiredFile("bin", "64bit", "obs.dll"));
            // Keep this module loaded for the worker process lifetime, including its resolver.
            var winrt = captureOnly ? NativeLibrary.Load(RequiredFile("bin", "64bit", "libobs-winrt.dll")) : 0;
            NativeLibrary.SetDllImportResolver(typeof(ObsNative).Assembly,
                (name, _, _) => name == ObsNative.Library ? library : name == ObsWinrtNative.Library ? winrt : 0);
            var config = Path.Combine(Path.GetTempPath(), "CultoMoveHost", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(config);
            if (!ObsNative.obs_startup("en-US", config, 0)) throw new InvalidOperationException("obs_startup failed.");
            _started = true;
            var dataPath = Path.Combine(Root, "data", "libobs");
            if (!Directory.Exists(dataPath)) throw new DirectoryNotFoundException(dataPath);
            // libobs check_path concatenates the directory and filename directly;
            // it does not add a separator. Without this slash every shader lookup
            // becomes "libobsdefault.effect" and graphics initialization fails.
            ObsNative.obs_add_data_path(dataPath.TrimEnd('\\', '/') + "/");
            var audio = new ObsNative.AudioInfo { SamplesPerSecond = 48000, Speakers = 2 };
            if (!ObsNative.obs_reset_audio(ref audio)) throw new InvalidOperationException("OBS audio initialization failed.");
            var graphics = Marshal.StringToCoTaskMemUTF8(RequiredFile("bin", "64bit", "libobs-d3d11.dll"));
            try
            {
                var video = new ObsNative.VideoInfo
                {
                    GraphicsModule = graphics, FpsNumerator = 30, FpsDenominator = 1,
                    BaseWidth = (uint)width, BaseHeight = (uint)height, OutputWidth = (uint)width, OutputHeight = (uint)height,
                    OutputFormat = 7, GpuConversion = false, ColorSpace = 3, Range = 2, ScaleType = 1
                };
                int result = ObsNative.obs_reset_video(ref video);
                if (result != 0) throw new InvalidOperationException($"OBS BGRA video initialization failed ({result}).");
                if (!ObsNative.obs_get_video_info(out var actual) || actual.OutputWidth != width || actual.OutputHeight != height || actual.OutputFormat != 7)
                    throw new InvalidOperationException("OBS did not accept the exact BGRA output dimensions.");
            }
            finally { Marshal.FreeCoTaskMem(graphics); }
            if (!captureOnly)
            {
                LoadModule("image-source");
                LoadModule("obs-transitions");
                LoadModule("move-transition");
                ObsNative.obs_post_load_modules();
                ObsLiveVideoSource.Register();
            }
        }
        catch { Dispose(); throw; }
    }

    private string RequiredFile(params string[] parts)
    {
        var path = Path.GetFullPath(Path.Combine(new[] { Root }.Concat(parts).ToArray()));
        if (!File.Exists(path)) throw new FileNotFoundException("Required OBS runtime file is missing.", path);
        return path;
    }

    private void LoadModule(string name)
    {
        var path = RequiredFile("obs-plugins", "64bit", name + ".dll");
        var data = Path.Combine(Root, "data", "obs-plugins", name);
        if (!Directory.Exists(data)) throw new DirectoryNotFoundException(data);
        int result = ObsNative.obs_open_module(out var module, path, data);
        if (result != 0 || module == 0) throw new InvalidOperationException($"OBS could not open {name} ({result}).");
        if (!ObsNative.obs_init_module(module)) throw new InvalidOperationException($"OBS could not initialize {name}.");
    }

    public void Dispose()
    {
        if (_started) { _started = false; ObsNative.obs_shutdown(); }
        foreach (var cookie in _searchDirectories) RemoveDllDirectory(cookie);
        _searchDirectories.Clear();
    }
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetDefaultDllDirectories(uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint AddDllDirectory(string path);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveDllDirectory(nint cookie);
}
