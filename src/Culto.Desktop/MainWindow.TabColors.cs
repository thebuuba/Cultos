using System.Globalization;
using System.Windows.Media;

namespace Culto.Desktop;

public partial class MainWindow
{
    private static readonly Dictionary<string, string> PanelTabPalette = new()
    {
        ["sources"] = "#FFE08A", ["bible"] = "#A7C7FF", ["youtube"] = "#D32F2F",
        ["media"] = "#F6C453", ["powerpoint"] = "#C43E1C", ["text"] = "#D3B6F0",
        ["playlists"] = "#9CDEC9"
    };
    private readonly Dictionary<string, SolidColorBrush> _panelTabBrushes = [];

    private SolidColorBrush GetPanelTabColor(string id)
    {
        if (_panelTabBrushes.TryGetValue(id, out var brush)) return brush;
        if (!PanelTabPalette.TryGetValue(id, out var hex))
        {
            if (!_workspace.PanelTabColors.TryGetValue(id, out hex) || hex is not { Length: 7 }
                || hex[0] != '#' || !int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                var used = PanelTabPalette.Values.Concat(_workspace.PanelTabColors.Values).ToHashSet(StringComparer.OrdinalIgnoreCase);
                do { hex = $"#{Random.Shared.Next(160, 241):X2}{Random.Shared.Next(160, 241):X2}{Random.Shared.Next(160, 241):X2}"; }
                while (used.Contains(hex));
                _workspace.PanelTabColors[id] = hex;
            }
        }
        brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        _panelTabBrushes.Add(id, brush);
        return brush;
    }
}
