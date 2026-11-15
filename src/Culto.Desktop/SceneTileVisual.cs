using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public static class SceneTileVisual
{
    public static Brush Background(string color) => new SolidColorBrush(ParseColor(color));
    public static Brush Ink(string color)
    {
        var value = ParseColor(color);
        double Linear(byte channel) { var c = channel / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        return .2126 * Linear(value.R) + .7152 * Linear(value.G) + .0722 * Linear(value.B) > .179
            ? new SolidColorBrush(Color.FromRgb(24, 35, 47)) : Brushes.White;
    }
    private static Color ParseColor(string color)
    {
        try { return (Color)ColorConverter.ConvertFromString(color); }
        catch { return Color.FromRgb(230, 243, 252); }
    }
    public static FrameworkElement Content(SceneTile scene)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        if (SceneArtwork.Decode(scene.ImageData) is { } image)
            panel.Children.Add(new Image { Source = image, Width = 38, Height = 30, Stretch = Stretch.Uniform });
        else
        {
            var kind = scene.Cue?.Kind.ToString().ToLowerInvariant() ?? scene.Action;
            var path = kind switch
            {
                "logo" => "M3 10 L12 3 L21 10 M5 9 L5 21 L10 21 L10 14 L14 14 L14 21 L19 21 L19 9",
                "blackout" => "M4 4 L20 4 Q22 4 22 6 L22 18 Q22 20 20 20 L4 20 Q2 20 2 18 L2 6 Q2 4 4 4 Z",
                "image" => "M3 3 L21 3 L21 21 L3 21 Z M3 16 L9 10 L15 16 L18 13 L21 16 M16 7 L17 7",
                "video" or "youtube" or "windowcapture" => "M4 4 L20 4 Q22 4 22 6 L22 18 Q22 20 20 20 L4 20 Q2 20 2 18 L2 6 Q2 4 4 4 Z M10 8 L16 12 L10 16 Z",
                "audio" => "M9 17 L9 5 L20 3 L20 15 M9 17 A3 3 0 1 1 6 14 A3 3 0 0 1 9 17 M20 15 A3 3 0 1 1 17 12 A3 3 0 0 1 20 15",
                "browser" or "browsersource" => "M22 12 A10 10 0 1 1 2 12 A10 10 0 1 1 22 12 M2 12 L22 12 M12 2 C5 7 5 17 12 22 C19 17 19 7 12 2 M4 6 L20 6 M4 18 L20 18",
                _ => "M5 3 L19 3 L19 21 L5 21 Z M9 7 L15 7 M9 12 L15 12 M9 17 L13 17"
            };
            panel.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse(path), Width = 30, Height = 28,
                Stretch = Stretch.Uniform, Stroke = Ink(scene.Color), StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center });
        }
        panel.Children.Add(new TextBlock { Text = scene.Label, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch });
        return panel;
    }
}
