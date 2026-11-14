using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AvalonDock.Controls;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunTypographyTests(MainWindow window)
    {
        RunTabTextTests(window);
        Test("Detached menus and tooltips share the console font and pixel layout", () =>
        {
            var font = (FontFamily)window.FindResource("ConsoleFont");
            var item = new MenuItem { Header = "Configuración y proyección" };
            var menu = new ContextMenu { PlacementTarget = window };
            menu.Items.Add(item);
            var tip = new ToolTip { Content = "Información del contenido", PlacementTarget = window };
            try
            {
                menu.IsOpen = true; tip.IsOpen = true; PumpFor(100);
                foreach (var control in new Control[] { menu, item, tip })
                {
                    Check(control.IsLoaded, "Typography check did not load " + control.GetType().Name);
                    Check(control.FontFamily.Equals(font), control.GetType().Name + " lost the console font outside its window");
                    Check(TextOptions.GetTextFormattingMode(control) == TextFormattingMode.Display && control.UseLayoutRounding,
                        control.GetType().Name + " does not align text to display pixels");
                }
            }
            finally { tip.IsOpen = false; menu.IsOpen = false; }
            foreach (var title in Descendants(window).OfType<AnchorablePaneTitle>().Where(t => t.IsVisible))
                Check(title.FontFamily.Equals(font), "Native panel caption lost the console font");
        });
        Test("Pastel selection keeps all order metadata readable", () =>
        {
            var list = (ListBox)window.FindName("PlanList");
            list.ScrollIntoView(list.Items[0]); list.UpdateLayout();
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            row.Focus(); Keyboard.Focus(row); PumpFor(80);
            Check(row.IsKeyboardFocusWithin, "Order row did not receive focus");
            var surface = (Border)row.Template.FindName("Row", row);
            foreach (var label in Descendants(row).OfType<TextBlock>().Where(t => t.IsVisible && t.Text.Length > 0))
                Check(ThemeContrast(label.Foreground, surface.Background) >= 4.5, "Pastel selection makes metadata unreadable: " + label.Text);
        });
    }
}
