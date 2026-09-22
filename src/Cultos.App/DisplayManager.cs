using Forms = System.Windows.Forms;

namespace Cultos.App;

public sealed class DisplayManager
{
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

    public static string Describe(Forms.Screen screen) =>
        $"{screen.Bounds.Width}×{screen.Bounds.Height}" + (screen.Primary ? " · Principal" : " · Externa");
}
