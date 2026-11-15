using System.Text.Json;
using Xunit;

namespace Culto.Core.Tests;

public sealed class CathedralOptionsTests
{
    [Fact]
    public void SavedCathedralProfileKeepsItsEffectAndControls()
    {
        var profile = JsonSerializer.Deserialize<TransitionOptions>("""{"AudioFadeEffect":"Cathedral","CathedralIntensityPercent":42,"CathedralTailMilliseconds":3700}""")!.Normalize();
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(profile));
        Assert.Equal("Cathedral", saved.RootElement.GetProperty("AudioFadeEffect").GetString());
        Assert.Equal(42, saved.RootElement.GetProperty("CathedralIntensityPercent").GetInt32());
        Assert.Equal(3700, saved.RootElement.GetProperty("CathedralTailMilliseconds").GetInt32());
    }

    [Fact]
    public void LegacyProfilesRemainDryAndInvalidCathedralValuesAreBounded()
    {
        using var legacy = JsonDocument.Parse(JsonSerializer.Serialize(JsonSerializer.Deserialize<TransitionOptions>("{}")!.Normalize()));
        Assert.Equal("Normal", legacy.RootElement.GetProperty("AudioFadeEffect").GetString());
        var bad = JsonSerializer.Deserialize<TransitionOptions>("""{"AudioFadeEffect":"unknown","CathedralIntensityPercent":999,"CathedralTailMilliseconds":99999}""")!.Normalize();
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(bad));
        Assert.Equal("Normal", saved.RootElement.GetProperty("AudioFadeEffect").GetString());
        Assert.Equal(60, saved.RootElement.GetProperty("CathedralIntensityPercent").GetInt32());
        Assert.Equal(8000, saved.RootElement.GetProperty("CathedralTailMilliseconds").GetInt32());
    }
}
