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
        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition());
        var kind = scene.Cue?.Kind.ToString().ToLowerInvariant() ?? scene.Action;
        var image = new Image { Source = SceneArtwork.Decode(scene.ImageData) ?? (ImageSource)Artwork(kind),
            MaxWidth = 68, MaxHeight = 68, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(image);
        if (!string.IsNullOrWhiteSpace(scene.Label))
        {
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock { Text = scene.Label, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                MaxHeight = 28, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
            Grid.SetRow(label, 1); panel.Children.Add(label);
        }
        return panel;
    }

    private static DrawingImage Artwork(string kind)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open())
        {
            Brush Brush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            void Shape(string data, string? fill, string? stroke = null, double width = 2) =>
                drawing.DrawGeometry(fill is null ? null : Brush(fill), stroke is null ? null : new Pen(Brush(stroke), width)
                    { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, Geometry.Parse(data));
            void Box(double x, double y, double w, double h, string color, double radius = 0) =>
                drawing.DrawRoundedRectangle(Brush(color), null, new Rect(x, y, w, h), radius, radius);
            void Circle(double x, double y, double radius, string color) => drawing.DrawEllipse(Brush(color), null, new Point(x, y), radius, radius);
            drawing.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 64, 64));
            switch (kind)
            {
                case "blackout": break;
                case "logo":
                    Shape("M3 55 Q17 47 30 53 L30 61 Q16 55 3 61 Z M34 53 Q47 47 61 55 L61 61 Q47 55 34 61 Z", "#126352");
                    Shape("M31 49 C16 41 28 30 36 20 C43 12 43 7 42 2 C53 17 38 28 31 36 C24 44 29 47 31 49 Z", "#F6AC00");
                    Shape("M35 46 C26 40 38 32 43 26 C49 20 51 16 51 12 C57 27 42 32 38 40 C36 44 38 47 40 49 Z", "#F6AC00");
                    Shape("M29 43 C17 36 26 27 30 23 C34 18 35 15 35 11 C43 23 27 29 26 36 C25 39 27 41 29 43 Z", "#FFC226");
                    Shape("M32 50 L32 62", null, "#FFFFFF", 2); break;
                case "video":
                    Shape("M7 53 L16 47 L48 47 L58 53 L56 59 L8 59 Z", "#EF8D00");
                    Shape("M16 50 L27 6 Q32 1 37 6 L49 50 Q33 58 16 50 Z", "#FF9D00");
                    Shape("M24 18 Q32 22 40 18 L43 29 Q32 34 21 29 Z", "#FFFFFF");
                    Shape("M19 38 Q32 44 45 38 L48 48 Q32 55 16 48 Z", "#FFFFFF"); break;
                case "youtube": case "windowcapture":
                    Box(2, 11, 60, 42, "#FF0000", 12);
                    Shape("M26 21 L44 32 L26 43 Z", "#FFFFFF"); break;
                case "bible":
                    Box(12, 4, 43, 55, "#9D5735", 3); Box(9, 4, 5, 55, "#BC7951", 1);
                    Box(16, 5, 2, 51, "#F4D6AC"); Box(19, 57, 34, 3, "#FFF1D5", 1);
                    Shape("M22 57 L22 64 L26 61 L30 64 L30 57 Z", "#438569");
                    Shape("M35 14 L35 34 M28 21 L42 21", null, "#FCE8C8", 3); break;
                case "image":
                    Circle(32, 32, 30, "#3C526B");
                    Shape("M24 6 L17 59 M40 6 L47 59 M24 55 L40 55", null, "#E7B875", 4);
                    Box(10, 15, 44, 34, "#F5F0E7", 3); Box(15, 20, 34, 24, "#DAE8EF", 1);
                    Shape("M19 39 L28 29 L34 34 L44 24", null, "#E85C57", 4); break;
                case "powerpoint":
                    Circle(38, 30, 25, "#F47A50"); Shape("M38 5 A25 25 0 0 1 63 30 L38 30 Z", "#FFAE8B");
                    Shape("M38 30 L63 30 A25 25 0 0 1 38 55 Z", "#D8431E");
                    Box(2, 17, 34, 36, "#C93813", 3);
                    Shape("M13 43 L13 26 L21 26 Q28 26 28 32 Q28 38 21 38 L14 38", null, "#FFFFFF", 4); break;
                case "browser": case "browsersource":
                    Circle(32, 32, 29, "#347BB4");
                    drawing.DrawEllipse(null, new Pen(Brush("#FFFFFF"), 2.5), new Point(32, 32), 24, 24);
                    Shape("M8 32 L56 32 M12 20 L52 20 M12 44 L52 44 M32 8 C15 20 15 44 32 56 C49 44 49 20 32 8 Z", null, "#FFFFFF", 2); break;
                case "audio":
                    Circle(32, 32, 29, "#2BA878");
                    Shape("M26 44 L26 17 L48 12 L48 38 M26 25 L48 20", null, "#FFFFFF", 4);
                    drawing.DrawEllipse(Brushes.White, null, new Point(20, 45), 7, 5);
                    drawing.DrawEllipse(Brushes.White, null, new Point(42, 39), 7, 5); break;
                case "scene":
                    Box(5, 7, 37, 37, "#326A9D", 5); Box(23, 24, 37, 34, "#EDB15D", 5);
                    Box(11, 13, 22, 4, "#FFFFFF", 2); Box(30, 31, 22, 4, "#FFFFFF", 2); break;
                default:
                    Box(13, 4, 39, 55, "#EAF2F8", 4); Box(13, 4, 6, 55, "#4289B2", 2);
                    Shape("M26 19 L44 19 M26 28 L44 28 M26 37 L44 37 M26 46 L37 46", null, "#4289B2", 3); break;
            }
        }
        group.Freeze(); var result = new DrawingImage(group); result.Freeze(); return result;
    }
}
