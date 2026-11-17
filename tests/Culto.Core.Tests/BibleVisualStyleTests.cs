using Culto.Core;
using Xunit;

public class BibleVisualStyleTests
{
    [Fact]
    public void ReferenceTransitionCanFollowTextOrPersistAnIndependentChoice()
    {
        var legacy = System.Text.Json.JsonSerializer.Deserialize<BibleVisualStyle>("""{"Transition":"Cut"}""")!.Normalize();
        Assert.Null(legacy.ReferenceTransition);
        Assert.False(legacy.UsesTextTransition);
        var separate = (legacy with { ReferenceTransition = "TextFocus" }).Normalize();
        Assert.True(separate.UsesTextTransition);
        var restored = System.Text.Json.JsonSerializer.Deserialize<BibleVisualStyle>(System.Text.Json.JsonSerializer.Serialize(separate))!.Normalize();
        Assert.Equal("Cut", restored.Transition);
        Assert.Equal("TextFocus", restored.ReferenceTransition);
        Assert.Equal("Cut", (separate with { ReferenceTransition = "Cut" }).Normalize().ReferenceTransition);
        Assert.Null((separate with { ReferenceTransition = "Invalid" }).Normalize().ReferenceTransition);
        Assert.Equal("System", (separate with { Transition = "Invalid" }).Normalize().Transition);
    }

    [Fact]
    public void IndependentReferenceColorAndHiddenVersionSurvivePersistenceWithLegacyDefaults()
    {
        var legacy = System.Text.Json.JsonSerializer.Deserialize<BibleVisualStyle>("""{"TextColor":"#aabbcc"}""")!.Normalize();
        Assert.Null(legacy.ReferenceColor);
        Assert.True(legacy.ShowVersion);
        var style = legacy with { ReferenceColor = "#ffe08a", ShowVersion = false };
        var restored = System.Text.Json.JsonSerializer.Deserialize<BibleVisualStyle>(
            System.Text.Json.JsonSerializer.Serialize(style))!.Normalize();
        Assert.Equal("#FFE08A", restored.ReferenceColor);
        Assert.Equal("#AABBCC", restored.TextColor);
        Assert.False(restored.ShowVersion);
        Assert.Null((style with { ReferenceColor = "invalid" }).Normalize().ReferenceColor);
    }

    [Theory]
    [InlineData("TextRise")]
    [InlineData("TextLines")]
    [InlineData("TextFocus")]
    public void TextTransitionsSurviveNormalizationAndPersistence(string transition)
    {
        var style = new BibleVisualStyle { Transition = transition, TransitionMilliseconds = 650 }.Normalize();
        Assert.Equal(transition, style.Transition);
        var restored = System.Text.Json.JsonSerializer.Deserialize<BibleVisualStyle>(System.Text.Json.JsonSerializer.Serialize(style))!.Normalize();
        Assert.Equal(transition, restored.Transition);
        Assert.Equal(650, restored.TransitionMilliseconds);
    }

    [Fact]
    public void ReferenceFontIsOptionalAndNormalizesWithoutChangingThePassageFont()
    {
        var style = System.Text.Json.JsonSerializer.Deserialize<BibleVisualStyle>(
            """{"FontFamily":"Georgia","ReferenceFontFamily":" Arial "}""")!;
        var json = System.Text.Json.JsonSerializer.Serialize(style.Normalize());
        Assert.Contains("\"ReferenceFontFamily\":\"Arial\"", json);
        Assert.Equal("Georgia", style.Normalize().FontFamily);
        var legacy = System.Text.Json.JsonSerializer.Deserialize<BibleVisualStyle>("""{"FontFamily":"Georgia"}""")!;
        Assert.Equal(new BibleVisualStyle { FontFamily = "Georgia" }, legacy.Normalize());
    }

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
