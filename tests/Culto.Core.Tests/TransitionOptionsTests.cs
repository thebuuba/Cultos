using System.Text.Json;
using Culto.Core;
using Xunit;

public class TransitionOptionsTests
{
    [Fact]
    public void MissingPersistedOptionsUseFastVisualAndIndependentAudioFades()
    {
        var options = JsonSerializer.Deserialize<TransitionOptions>("{}")!;
        Assert.Equal("Dissolve", options.Kind);
        Assert.Equal(300, options.DurationMilliseconds);
        Assert.Equal(250, options.AudioFadeOutMilliseconds);
        Assert.Equal(250, options.AudioFadeInMilliseconds);
        Assert.Equal("System", new BibleVisualStyle().Transition);
    }

    [Theory]
    [InlineData("Cut")]
    [InlineData("Dissolve")]
    [InlineData("Slide")]
    [InlineData("Zoom")]
    [InlineData("Move")]
    public void SupportedKindsAndIndependentDurationsSurvivePersistence(string kind)
    {
        var options = new TransitionOptions { Kind = kind, DurationMilliseconds = 780,
            AudioFadeOutMilliseconds = 2400, AudioFadeInMilliseconds = 600 };
        var restored = JsonSerializer.Deserialize<TransitionOptions>(JsonSerializer.Serialize(options))!.Normalize();
        Assert.Equal(options, restored);
    }

    [Theory]
    [InlineData(null, -1, -1, int.MaxValue, "Dissolve", 100, 0, 5000)]
    [InlineData("invalid", int.MaxValue, int.MaxValue, -1, "Dissolve", 3000, 5000, 0)]
    [InlineData("Cut", 100, 0, 0, "Cut", 100, 0, 0)]
    public void InvalidPersistedValuesAreClampedWithoutChangingTheirSource(string? kind, int duration,
        int fadeOut, int fadeIn, string expectedKind, int expectedDuration, int expectedOut, int expectedIn)
    {
        var original = new TransitionOptions { Kind = kind!, DurationMilliseconds = duration,
            AudioFadeOutMilliseconds = fadeOut, AudioFadeInMilliseconds = fadeIn };
        var normalized = original.Normalize();
        Assert.Equal(expectedKind, normalized.Kind);
        Assert.Equal(expectedDuration, normalized.DurationMilliseconds);
        Assert.Equal(expectedOut, normalized.AudioFadeOutMilliseconds);
        Assert.Equal(expectedIn, normalized.AudioFadeInMilliseconds);
        Assert.Equal(kind, original.Kind);
        Assert.Equal(duration, original.DurationMilliseconds);
        Assert.Equal(fadeOut, original.AudioFadeOutMilliseconds);
        Assert.Equal(fadeIn, original.AudioFadeInMilliseconds);
    }

    [Fact]
    public void NormalizationReturnsASnapshotThatSurvivesLaterSettingsEdits()
    {
        var source = new TransitionOptions { Kind = "Slide", DurationMilliseconds = 700,
            AudioFadeOutMilliseconds = 2000, AudioFadeInMilliseconds = 800 };
        var snapshot = source.Normalize();
        source.Kind = "Cut";
        source.DurationMilliseconds = 100;
        source.AudioFadeOutMilliseconds = source.AudioFadeInMilliseconds = 0;
        Assert.Equal("Slide", snapshot.Kind);
        Assert.Equal(700, snapshot.DurationMilliseconds);
        Assert.Equal(2000, snapshot.AudioFadeOutMilliseconds);
        Assert.Equal(800, snapshot.AudioFadeInMilliseconds);
    }

    [Fact]
    public void OldFactorySettingsUpgradeWithoutMutatingTheLoadedObject()
    {
        var old = JsonSerializer.Deserialize<TransitionOptions>("""
            {"Kind":"Dissolve","DurationMilliseconds":500,"AudioFadeOutMilliseconds":1500,"AudioFadeInMilliseconds":1500}
            """)!;
        var upgraded = old.UpgradeStoredDefaults(0);
        Assert.Equal("Dissolve", upgraded.Kind);
        Assert.Equal(300, upgraded.DurationMilliseconds);
        Assert.Equal(250, upgraded.AudioFadeOutMilliseconds);
        Assert.Equal(250, upgraded.AudioFadeInMilliseconds);
        Assert.Equal(500, old.DurationMilliseconds);
        Assert.Equal(1500, old.AudioFadeOutMilliseconds);
        Assert.Equal(1500, old.AudioFadeInMilliseconds);
    }

    [Theory]
    [InlineData("Dissolve", 550, 1500, 1500, 0)]
    [InlineData("Dissolve", 500, 1450, 1500, 0)]
    [InlineData("Dissolve", 500, 1500, 1450, 0)]
    [InlineData("Move", 500, 1500, 1500, 0)]
    [InlineData("Dissolve", 500, 1500, 1500, 1)]
    [InlineData("Dissolve", 500, 1500, 1500, 2)]
    public void UpgradePreservesCustomSettingsAndAlreadyMigratedWorkspaces(string kind, int duration,
        int fadeOut, int fadeIn, int version)
    {
        var original = new TransitionOptions { Kind = kind, DurationMilliseconds = duration,
            AudioFadeOutMilliseconds = fadeOut, AudioFadeInMilliseconds = fadeIn };
        var upgraded = original.UpgradeStoredDefaults(version);
        Assert.Equal(original, upgraded);
        Assert.NotSame(original, upgraded);
    }

    [Fact]
    public void UpgradeStillSanitizesInvalidStoredValues()
    {
        var upgraded = new TransitionOptions { Kind = "unsupported", DurationMilliseconds = -1,
            AudioFadeOutMilliseconds = -1, AudioFadeInMilliseconds = 9000 }.UpgradeStoredDefaults(0);
        Assert.Equal("Dissolve", upgraded.Kind);
        Assert.Equal(100, upgraded.DurationMilliseconds);
        Assert.Equal(0, upgraded.AudioFadeOutMilliseconds);
        Assert.Equal(5000, upgraded.AudioFadeInMilliseconds);
    }
}
