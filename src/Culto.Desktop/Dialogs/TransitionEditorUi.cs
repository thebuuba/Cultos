using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Culto.Desktop.Dialogs;

internal static class TransitionEditorUi
{
    public static readonly Brush Lime = Brush("#BEF264"), Violet = Brush("#B18CF2"), Cyan = Brush("#27C3DE"), Muted = Brush("#A3A3A3");
    public static SolidColorBrush Brush(string color) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
    public static TextBlock Text(string text, double size = 13, Brush? color = null, bool bold = false) => new()
    { Text = text, FontSize = size, Foreground = color ?? Brushes.WhiteSmoke, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    public static Border Card(UIElement child, double padding = 18) => new()
    { Child = child, Background = Brush("#242424"), BorderBrush = Brush("#353535"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(24), Padding = new Thickness(padding) };
    public static Button Button(string title, string name, Action action, bool primary = false)
    {
        var button = new Button { Content = title, Name = name };
        button.SetResourceReference(FrameworkElement.StyleProperty, "EditorButton");
        if (primary) { button.Background = Lime; button.Foreground = Brush("#171717"); }
        button.Click += (_, _) => action(); return button;
    }
}
