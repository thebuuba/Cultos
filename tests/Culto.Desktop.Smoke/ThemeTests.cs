using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunThemeTests(MainWindow window)
    {
        Test("Console font is bundled and resolves all four weights offline", () =>
        {
            foreach (var weight in new[] { FontWeights.Normal, FontWeights.Medium, FontWeights.SemiBold, FontWeights.Bold })
            {
                var face = new Typeface(window.FontFamily, FontStyles.Normal, weight, FontStretches.Normal);
                Check(face.TryGetGlyphTypeface(out var glyphs), "Font did not resolve");
                Check(glyphs!.FontUri.Scheme == "pack" && glyphs.FamilyNames.Values.Any(name => name.Contains("Poppins")),
                    "Console fell back to a system font instead of bundled Poppins");
                Check(glyphs.Weight == weight, "Font weight was synthesized");
            }
        });

        Test("Rapid tab changes finish visible without changing live content", () =>
        {
            var tabs = (TabControl)window.FindName("ContentTabs");
            var session = Field<PresentationSession>(window, "_session");
            var live = session.Live;
            var original = tabs.SelectedIndex;
            for (var i = 0; i < 15; i++) tabs.SelectedIndex = i % 3;
            tabs.SelectedIndex = original;
            PumpFor(300);
            Check(ReferenceEquals(live, session.Live), "Navigation changed projected content");
            foreach (var tab in tabs.Items.Cast<TabItem>())
            {
                var content = (FrameworkElement)tab.Content;
                Check(content.Opacity == 1, "A tab was left faded after interrupted transitions");
                Check(content.RenderTransform is not TranslateTransform move || move.Y == 0, "A tab was left displaced");
            }
            Check(((FrameworkElement)((TabItem)tabs.SelectedItem).Content).IsVisible, "Selected tab is hidden");
        });

        Test("Selected tab colors do not make Bible content unreadable", () =>
        {
            var tabs = (TabControl)window.FindName("ContentTabs");
            var original = tabs.SelectedIndex;
            try
            {
                tabs.SelectedIndex = 1;
                var title = (TextBlock)window.FindName("BibleNameText");
                Check(((SolidColorBrush)title.Foreground).Color == ((SolidColorBrush)window.FindResource("TextBrush")).Color,
                    "Selected header foreground leaked into Bible content");
            }
            finally { tabs.SelectedIndex = original; window.UpdateLayout(); }
        });

        Test("Themed media slider retains native commands and value binding", () =>
        {
            var slider = (Slider)window.FindName("VolumeSlider");
            var original = slider.Value;
            slider.ApplyTemplate();
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            Check(track?.Thumb is not null, "Native slider track/thumb is missing");
            slider.Value = 30;
            Slider.IncreaseSmall.Execute(null, slider);
            Check(slider.Value == 30 + slider.SmallChange, "Keyboard command did not change slider value");
            Check(track!.Value == slider.Value, "Track did not follow slider value");
            track.Value = 45;
            Check(slider.Value == 45, "Dragging track would not update slider value");
            slider.Value = original;
        });
    }
}
