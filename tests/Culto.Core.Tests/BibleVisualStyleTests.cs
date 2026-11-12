using Culto.Core;
using Xunit;

public class BibleVisualStyleTests
{
    [Fact]
    public void InvalidPersistedValuesFallBackToReadableSafeStyle()
    {
        var style = new BibleVisualStyle { FontFamily = "", FontSize = double.NaN, TextColor = "red",
            BackgroundColor = "invalid", BackgroundDim = double.PositiveInfinity, TextEffect = "Invalid",
            Alignment = "Invalid", Transition = "Invalid", TransitionMilliseconds = int.MaxValue };
        var value = style.Normalize();
        Assert.Equal("Segoe UI", value.FontFamily);
        Assert.Equal(76, value.FontSize);
        Assert.Equal("#FFFFFF", value.TextColor);
        Assert.Equal("#101722", value.BackgroundColor);
        Assert.Equal(.35, value.BackgroundDim);
        Assert.Equal("None", value.TextEffect);
        Assert.Equal("Center", value.Alignment);
        Assert.Equal("System", value.Transition);
        Assert.Equal(2000, value.TransitionMilliseconds);
        Assert.True(double.IsNaN(style.FontSize));
    }

    [Fact]
    public void NormalizationCreatesAnIndependentSnapshotAndRetainsChosenStyle()
    {
        var original = new BibleVisualStyle { FontFamily = " Georgia ", FontSize = 96, TextColor = "#aabbcc",
            Bold = true, Italic = true, Alignment = "Right", TextEffect = "Shadow", Transition = "Cut",
            BackgroundImagePath = "background.png", BackgroundDim = .6 };
        var copy = original.Normalize();
        original.FontFamily = "Arial";
        Assert.Equal("Georgia", copy.FontFamily);
        Assert.Equal("#AABBCC", copy.TextColor);
        Assert.Equal("background.png", copy.BackgroundImagePath);
        Assert.Equal("Right", copy.Alignment);
        Assert.Equal("Shadow", copy.TextEffect);
        Assert.Equal("Cut", copy.Transition);
        Assert.True(copy.Bold && copy.Italic);
        Assert.Equal(96, copy.FontSize);
    }
}
