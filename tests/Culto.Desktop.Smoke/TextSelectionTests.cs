using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunTextSelectionTests(MainWindow window)
    {
        Test("Focusing the transparent service name keeps its text readable", () =>
        {
            var name = (TextBox)window.FindName("PlanNameBox");
            window.Activate(); name.Focus(); Keyboard.Focus(name); name.Select(name.Text.Length, 0);
            window.UpdateLayout(); PumpFor(100);
            Check(name.IsKeyboardFocused, "Service name did not receive focus");
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "text-focus-service-name.png"));
            Check(ThemeContrast(name.Foreground, (Brush)window.FindResource("PanelBrush")) >= 4.5,
                "Focused transparent service name turns dark against its panel");
        });
        Test("Selected text stays legible in fields, notes and editable selectors", () =>
        {
            var single = new TextBox { Text = "Texto seleccionado 123", FontSize = 20 };
            var note = new TextBox { Text = "Texto seleccionado 123", FontSize = 20, AcceptsReturn = true,
                Foreground = Brushes.White, Background = Brushes.Transparent, Height = 100 };
            var combo = new ComboBox { IsEditable = true, Text = "Texto seleccionado 123", FontSize = 20 };
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(single); panel.Children.Add(note); panel.Children.Add(combo);
            var dialog = new Window { Owner = window, Width = 540, Height = 330, Content = panel,
                Background = (Brush)window.FindResource("PanelBrush"), Topmost = true };
            try
            {
                dialog.Show(); dialog.Activate(); dialog.UpdateLayout();
                combo.ApplyTemplate();
                var editable = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
                foreach (var (name, text) in new[] { ("campo", single), ("nota", note), ("selector", editable) })
                {
                    text.Focus(); Keyboard.Focus(text); text.SelectAll(); dialog.UpdateLayout(); PumpFor(150);
                    Check(text.SelectionLength == text.Text.Length && text.IsKeyboardFocused, "Selection fixture is not focused");
                    var first = text.GetRectFromCharacterIndex(0);
                    var last = text.GetRectFromCharacterIndex(text.Text.Length - 1, true);
                    var origin = text.TranslatePoint(new Point(first.Left, first.Top), dialog);
                    var image = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    image.Render(dialog);
                    var pixels = new byte[image.PixelWidth * image.PixelHeight * 4];
                    image.CopyPixels(pixels, image.PixelWidth * 4, 0);
                    var dark = 0;
                    for (var y = (int)origin.Y + 2; y < origin.Y + first.Height - 2; y++)
                        for (var x = (int)origin.X + 2; x < origin.X + last.Right - first.Left - 2; x++)
                        {
                            var i = (y * image.PixelWidth + x) * 4;
                            if (pixels[i] < 65 && pixels[i + 1] < 65 && pixels[i + 2] < 65) dark++;
                        }
                    CaptureDesignWindow(dialog, Path.Combine(Environment.CurrentDirectory, ".tools", "text-selection-" + name + ".png"));
                    Check(dark > 40, "Selection covers the rendered letters in " + name + " (glyph pixels: " + dark + ")");
                }
            }
            finally { dialog.Close(); }
        });
    }
}
