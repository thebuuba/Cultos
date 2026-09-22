using System.IO;
using System.Text.Json;

namespace Cultos.App;

public sealed class AppSettings
{
    public double? MainLeft { get; set; }
    public double? MainTop { get; set; }
    public double MainWidth { get; set; } = 1440;
    public double MainHeight { get; set; } = 900;
    public bool MainMaximized { get; set; }
    public string LastMode { get; set; } = "Bible";
    public string? DisplayDeviceName { get; set; }
    public double MediaVolume { get; set; } = 0.8;
    public bool MonitorPanelVisible { get; set; } = true;
    public bool RemoteControlEnabled { get; set; }
    public int RemoteControlPort { get; set; } = 8777;
    public string RemoteControlPin { get; set; } = "";
}

public sealed class AppSettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public AppSettingsStore(string dataFolder)
    {
        Directory.CreateDirectory(dataFolder);
        _path = Path.Combine(dataFolder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), _options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo leer settings.json", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, _options));
            File.Move(temp, _path, true);
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo guardar settings.json", ex);
        }
    }
}
