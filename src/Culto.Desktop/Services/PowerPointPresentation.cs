using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace Culto.Desktop.Services;

// PowerPoint owns the rendering, builds, transitions and embedded media. Only our
// untitled read-only copy is closed; an existing user's presentation is never owned.
public sealed partial class PowerPointPresentation : IDisposable
{
    private dynamic? _application, _presentation, _show;
    private PowerPointToolbarSuppressor? _toolbarSuppressor;
    private readonly HashSet<int> _pausedMedia = [];
    private int _pausedMediaSlideId;
    private bool _isPaused;
    public IntPtr Handle { get; private set; }
    public int SlideCount { get; private set; }
    public int SlideIndex => _show is null ? 0 : (int)_show.View.CurrentShowPosition;
    public int ClickIndex => _show is null ? 0 : (int)_show.View.GetClickIndex();
    public bool IsRunning => Handle != IntPtr.Zero && IsWindow(Handle);
    public static bool IsAvailable => Type.GetTypeFromProgID("PowerPoint.Application") is not null;

    public void Open(string path)
    {
        if (_presentation is not null) throw new InvalidOperationException("La presentación ya está abierta.");
        var type = Type.GetTypeFromProgID("PowerPoint.Application")
            ?? throw new InvalidOperationException("Instala Microsoft PowerPoint para presentar con animaciones y videos.");
        try
        {
            _application = Activator.CreateInstance(type)!;
            int security = (int)_application.AutomationSecurity;
            try
            {
                _application.AutomationSecurity = 3;
                _presentation = _application.Presentations.Open(Path.GetFullPath(path), -1, -1, 0);
            }
            finally { _application.AutomationSecurity = security; }
            SlideCount = (int)_presentation.Slides.Count;
            if (SlideCount == 0) throw new InvalidDataException("La presentación no contiene diapositivas.");
            CaptureAudioChannels();
        }
        catch { Dispose(); throw; }
    }

    public void Start(DisplayInfo? display)
    {
        if (_presentation is null) throw new InvalidOperationException("No hay presentación preparada.");
        var previousFocus = CaptureForeground();
        var previousWindows = TopLevelWindows();
        try
        {
            dynamic settings = _presentation.SlideShowSettings;
            try
            {
                settings.ShowType = 4; // ppShowTypeWindow2 stays running while the operator uses another app.
                settings.ShowWithAnimation = -1;
                _show = settings.Run();
            }
            finally { Release(settings); }
            // Some PowerPoint versions expose HWND in the type library but reject
            // IDispatch access. Office can reuse its empty application frame for
            // this show, so ownership includes an existing frame renamed by Run.
            Handle = FindOwnedShowWindow(previousWindows, (string)_presentation.Name);
            if (Handle == IntPtr.Zero) throw new InvalidOperationException("PowerPoint no entregó una ventana de presentación independiente.");
            _toolbarSuppressor = PowerPointToolbarSuppressor.Capture(Handle);
            var style = GetWindowLong(Handle, -16);
            SetWindowLong(Handle, -16, style & ~0x00FF0000); // No frame, caption, sizing border or scrollbars.
            var extended = GetWindowLong(Handle, -20);
            SetWindowLong(Handle, -20, (extended | 0x08000080) & ~0x00040000); // NOACTIVATE, TOOLWINDOW.
            Place(display);
        }
        finally { RestoreForeground(previousFocus); }
    }

    public void Place(DisplayInfo? display)
    {
        if (!IsRunning) return;
        var screens = System.Windows.Forms.SystemInformation.VirtualScreen;
        var left = display?.Left ?? screens.Left - 4000;
        var top = display?.Top ?? screens.Top;
        var width = display?.Width ?? 1920;
        var height = display?.Height ?? 1080;
        if (!SetWindowPos(Handle, new IntPtr(display is null ? -2 : -1), left, top, width, height, 0x0010 | 0x0020 | 0x0040))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        Projection.NativePresentationWindow.FitContent(Handle);
    }

    public bool Advance(int direction)
    {
        if (!IsRunning || _show is null) return false;
        var previousFocus = CaptureForeground();
        dynamic view = _show.View;
        try
        {
            int click = (int)view.GetClickIndex(), count = (int)view.GetClickCount();
            int slide = (int)view.CurrentShowPosition;
            if (direction > 0)
            {
                // Next executes the build; GotoClick can leave the background window's pixels stale.
                if (click < count || slide < SlideCount) view.Next();
                else return false; // Keep the last slide on screen.
            }
            else
            {
                if (click > 0 || slide > 1) view.Previous();
                else return false;
            }
            return true;
        }
        finally { Release(view); RestoreForeground(previousFocus); }
    }

    public void SetPaused(bool paused)
    {
        if (_show is null || !IsRunning || (!paused && !_isPaused)) return;
        var previousFocus = CaptureForeground();
        dynamic view = _show.View;
        dynamic? slide = null, shapes = null;
        try
        {
            slide = view.Slide;
            int slideId = (int)slide.SlideID;
            if (paused)
            {
                if (!_isPaused || _pausedMediaSlideId != slideId) _pausedMedia.Clear();
                _pausedMediaSlideId = slideId;
                _isPaused = true;
                shapes = slide.Shapes;
                for (int index = 1; index <= (int)shapes.Count; index++)
                {
                    dynamic shape = shapes[index];
                    dynamic? player = null;
                    try
                    {
                        if ((int)shape.Type != 16) continue; // msoMedia
                        int shapeId = (int)shape.Id;
                        player = view.Player(shapeId);
                        if ((int)player.State != 0) continue; // ppPlaying; leave stopped/not-ready media untouched.
                        player.Pause();
                        _pausedMedia.Add(shapeId);
                    }
                    finally { Release(player); Release(shape); }
                }
                view.State = 2; // ppSlideShowPaused does not reliably pause embedded media itself.
            }
            else
            {
                view.State = 1; // ppSlideShowRunning
                if (slideId == _pausedMediaSlideId)
                {
                    foreach (var shapeId in _pausedMedia)
                    {
                        dynamic player = view.Player(shapeId);
                        try
                        {
                            if ((int)player.State == 1) player.Play(); // Only media paused by this service.
                        }
                        finally { Release(player); }
                    }
                }
                _pausedMedia.Clear();
                _isPaused = false;
            }
        }
        finally
        {
            Release(shapes); Release(slide); Release(view);
            SuppressOwnedPauseToolbar();
            RestoreForeground(previousFocus);
        }
    }

    // Also called by the console's existing timer: Office may show the pause bar
    // later when a modal opens. Unattributed/global Office windows remain untouched.
    public void SuppressOwnedPauseToolbar()
    {
        if (_isPaused && IsRunning) _toolbarSuppressor?.HideAttributedToolbars();
    }

    public void Restart()
    {
        if (_show is null || !IsRunning) return;
        var previousFocus = CaptureForeground();
        dynamic view = _show.View;
        try
        {
            // Restart resets native builds and media; no paused player from the old slide may resume later.
            _pausedMedia.Clear();
            _isPaused = false;
            view.GotoSlide(1, -1);
            view.State = 1;
        }
        finally { Release(view); RestoreForeground(previousFocus); }
    }
    public void ExportPreview(string path) => _presentation?.Slides[1].Export(path, "PNG", 1280, 720);

    public void Dispose()
    {
        _toolbarSuppressor = null;
        if (IsRunning) ShowWindow(Handle, 0);
        try { if (_show is not null) _show.View.Exit(); } catch (COMException) { }
        try { DisposeAudioChannels(); } catch (Exception error) when (error is COMException or AggregateException) { }
        try { if (_presentation is not null) { _presentation.Saved = -1; _presentation.Close(); } } catch (COMException) { }
        Release(_show); Release(_presentation); Release(_application);
        _show = _presentation = _application = null; Handle = IntPtr.Zero;
    }

    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    private readonly record struct ForegroundSnapshot(IntPtr Window, uint InputTick);

    private static ForegroundSnapshot CaptureForeground()
    {
        var input = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref input) ? new ForegroundSnapshot(GetForegroundWindow(), input.Tick) : default;
    }

    private void RestoreForeground(ForegroundSnapshot snapshot)
    {
        var showHandle = Handle;
        if (showHandle == IntPtr.Zero || snapshot.Window == IntPtr.Zero) return;
        void RestoreIfOwned()
        {
            if (Handle != showHandle || !IsRunning || !IsWindow(snapshot.Window)) return;
            var input = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref input) || input.Tick != snapshot.InputTick) return;
            var foreground = GetForegroundWindow();
            if (foreground == snapshot.Window) return;
            // PowerPoint may activate its native show or the Culto window hosting
            // it. Never override new physical input or another app's activation.
            if (foreground != showHandle && foreground != GetAncestor(showHandle, 2)) return;
            SetForegroundWindow(snapshot.Window);
        }
        RestoreIfOwned();
        // Embedded media can activate asynchronously after the COM call returns.
        // One guarded dispatcher replay handles that without a polling focus lock.
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RestoreIfOwned));
    }

    [StructLayout(LayoutKind.Sequential)] private struct LastInputInfo { public uint Size, Tick; }
    private static Dictionary<IntPtr, string> TopLevelWindows()
    {
        var handles = new Dictionary<IntPtr, string>();
        EnumWindows((handle, _) =>
        {
            var title = new StringBuilder(1024); GetWindowText(handle, title, title.Capacity);
            handles.Add(handle, title.ToString());
            return true;
        }, IntPtr.Zero);
        return handles;
    }
    private static IntPtr FindOwnedShowWindow(Dictionary<IntPtr, string> before, string name)
    {
        var candidates = new List<IntPtr>();
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var processId);
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById((int)processId);
                if (!process.ProcessName.Equals("POWERPNT", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (ArgumentException) { return true; }
            var type = new StringBuilder(256); GetClassName(handle, type, type.Capacity);
            var title = new StringBuilder(1024); GetWindowText(handle, title, title.Capacity);
            var existed = before.TryGetValue(handle, out var previousTitle);
            if (!existed && type.ToString().Equals("screenClass", StringComparison.OrdinalIgnoreCase)
                || type.ToString() == "PPTFrameClass" && title.ToString().Contains(name, StringComparison.OrdinalIgnoreCase)
                    && (!existed || !string.Equals(previousTitle, title.ToString(), StringComparison.Ordinal)))
                candidates.Add(handle);
            return true;
        }, IntPtr.Zero);
        return candidates.Count == 1 ? candidates[0] : IntPtr.Zero;
    }
    private delegate bool EnumWindowCallback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr handle, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr handle, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int w, int h, uint flags);
}
