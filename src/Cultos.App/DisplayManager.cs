using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Cultos.App;

public sealed class DisplayManager
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly AppSettings _settings;

    public DisplayManager(AppSettings settings) => _settings = settings;

    public IReadOnlyList<Forms.Screen> Screens => Forms.Screen.AllScreens;

    public Forms.Screen ResolvePresentationScreen()
    {
        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0) throw new InvalidOperationException("Windows no informó ninguna pantalla disponible.");

        var configured = !string.IsNullOrWhiteSpace(_settings.DisplayDeviceName)
            ? screens.FirstOrDefault(s => string.Equals(s.DeviceName, _settings.DisplayDeviceName, StringComparison.OrdinalIgnoreCase))
            : null;

        return configured ?? screens.FirstOrDefault(s => !s.Primary) ?? screens[0];
    }

    public bool IsConfiguredScreenAvailable() =>
        string.IsNullOrWhiteSpace(_settings.DisplayDeviceName) ||
        Forms.Screen.AllScreens.Any(s => string.Equals(s.DeviceName, _settings.DisplayDeviceName, StringComparison.OrdinalIgnoreCase));

    public void Select(string deviceName)
    {
        if (!Forms.Screen.AllScreens.Any(s => string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("La pantalla seleccionada ya no está disponible.", nameof(deviceName));

        _settings.DisplayDeviceName = deviceName;
    }

    public void PlacePresentationWindow(Window window, Forms.Screen screen)
    {
        window.WindowState = WindowState.Normal;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.ShowInTaskbar = false;
        window.Topmost = true;
        if (!window.IsVisible) window.Show();

        var handle = new WindowInteropHelper(window).Handle;
        var bounds = screen.Bounds;
        SetWindowPos(
            handle,
            HwndTopmost,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SwpNoActivate | SwpShowWindow);
    }

    public void PlaceTestWindow(Window window, Forms.Screen screen)
    {
        window.WindowState = WindowState.Normal;
        window.WindowStyle = WindowStyle.SingleBorderWindow;
        window.ResizeMode = ResizeMode.CanResize;
        window.ShowInTaskbar = true;
        window.Topmost = false;
        if (!window.IsVisible) window.Show();

        var area = screen.WorkingArea;
        var width = Math.Min(1100, Math.Max(720, (int)(area.Width * 0.78)));
        var height = (int)(width * 9d / 16d);
        if (height > area.Height * 0.78)
        {
            height = (int)(area.Height * 0.78);
            width = (int)(height * 16d / 9d);
        }

        var x = area.Left + (area.Width - width) / 2;
        var y = area.Top + (area.Height - height) / 2;
        var handle = new WindowInteropHelper(window).Handle;
        SetWindowPos(handle, IntPtr.Zero, x, y, width, height, SwpNoActivate | SwpShowWindow);
    }

    public void IdentifyScreens()
    {
        var screens = Forms.Screen.AllScreens;
        for (var index = 0; index < screens.Length; index++)
        {
            var screen = screens[index];
            var label = new TextBlock
            {
                Text = $"Pantalla {index + 1}\n{Describe(screen)}",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            var window = new Window
            {
                Width = 340,
                Height = 170,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 25, 22)),
                Content = label
            };

            window.Show();
            var handle = new WindowInteropHelper(window).Handle;
            var x = screen.Bounds.Left + (screen.Bounds.Width - 340) / 2;
            var y = screen.Bounds.Top + (screen.Bounds.Height - 170) / 2;
            SetWindowPos(handle, HwndTopmost, x, y, 340, 170, SwpNoActivate | SwpShowWindow);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.Close();
            };
            timer.Start();
        }
    }

    public static string Describe(Forms.Screen screen) =>
        $"{screen.Bounds.Width}×{screen.Bounds.Height}" + (screen.Primary ? " · Principal" : " · Externa");

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
