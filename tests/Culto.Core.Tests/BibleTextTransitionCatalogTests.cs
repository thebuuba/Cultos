using System.Text.Json;
using Culto.Core;
using Xunit;

public class BibleTextTransitionCatalogTests
{
    [Fact]
    public void CatalogProvidesEightVariantsForEveryMotionAndScopeWithUniqueIds()
    {
        var all = BibleTextTransitionCatalog.All;
        Assert.Equal(291, all.Count);
        Assert.Equal(291, all.Select(preset => preset.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(291, all.Select(preset => preset.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(12, all.Select(preset => preset.Family).Distinct().Count());
        var generated = all.Where(preset => preset.Id.StartsWith("TextPreset:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(288, generated.Length);
        foreach (var motion in Enum.GetValues<BibleTextMotion>())
        foreach (var scope in Enum.GetValues<BibleTextScope>())
        {
            var variants = generated.Where(preset => preset.Motion == motion && preset.Scope == scope).ToArray();
            Assert.Equal(Enumerable.Range(0, 8), variants.Select(preset => preset.Variant));
            Assert.All(variants, preset =>
            {
                Assert.False(string.IsNullOrWhiteSpace(preset.Name));
                Assert.False(string.IsNullOrWhiteSpace(preset.Family));
            });
        }
    }

    [Fact]
    public void EveryCatalogIdResolvesAndSurvivesStylePersistence()
    {
        foreach (var preset in BibleTextTransitionCatalog.All)
        {
            Assert.True(BibleTextTransitionCatalog.TryGet(preset.Id, out var resolved));
            Assert.Equal(preset, resolved);
            var style = new BibleVisualStyle { Transition = preset.Id }.Normalize();
            var restored = JsonSerializer.Deserialize<BibleVisualStyle>(JsonSerializer.Serialize(style))!.Normalize();
            Assert.Equal(preset.Id, restored.Transition);
            Assert.True(restored.UsesTextTransition);
        }
    }

    [Theory]
    [InlineData("TextRise", BibleTextMotion.Slide, BibleTextScope.Passage, "Subida suave")]
    [InlineData("TextLines", BibleTextMotion.Slide, BibleTextScope.Lines, "Entrada por líneas")]
    [InlineData("TextFocus", BibleTextMotion.Blur, BibleTextScope.Passage, "Enfoque suave")]
    public void LegacyIdsKeepTheirNamesAndDescriptors(string id, BibleTextMotion motion, BibleTextScope scope, string name)
    {
        Assert.True(BibleTextTransitionCatalog.TryGet(id, out var preset));
        Assert.Equal(name, preset.Name);
        Assert.Equal(motion, preset.Motion);
        Assert.Equal(scope, preset.Scope);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" TextRise ")]
    [InlineData("textrise")]
    [InlineData("TextPreset:Unknown:Words:0")]
    [InlineData("TextPreset:Slide:Unknown:0")]
    [InlineData("TextPreset:Slide:Words:-1")]
    [InlineData("TextPreset:Slide:Words:8")]
    [InlineData("TextPreset:Slide:Words:00")]
    [InlineData("TextPreset:slide:Words:0")]
    [InlineData("TextPreset:Slide:Words:0:extra")]
    public void MalformedIdsAreRejectedAndNormalizeToSystem(string? id)
    {
        Assert.False(BibleTextTransitionCatalog.TryGet(id, out _));
        var style = new BibleVisualStyle { Transition = id! };
        Assert.False(style.UsesTextTransition);
        Assert.Equal("System", style.Normalize().Transition);
    }

    [Fact]
    public void FavoritesNormalizeValidDistinctIdsWithoutChangingInputAndPersist()
    {
        const string input = " |TextRise|invalid| TextPreset:Slide:Words:0 |TextRise|Cut|TextFocus|";
        var source = new BibleVisualStyle { FavoriteTransitions = input };
        var normalized = source.Normalize();
        Assert.Equal("TextRise|TextPreset:Slide:Words:0|TextFocus", normalized.FavoriteTransitions);
        Assert.Equal(input, source.FavoriteTransitions);
        var restored = JsonSerializer.Deserialize<BibleVisualStyle>(JsonSerializer.Serialize(normalized))!.Normalize();
        Assert.Equal(normalized.FavoriteTransitions, restored.FavoriteTransitions);
        Assert.Equal("", new BibleVisualStyle { FavoriteTransitions = null! }.Normalize().FavoriteTransitions);
        Assert.Equal("", JsonSerializer.Deserialize<BibleVisualStyle>("{}")!.Normalize().FavoriteTransitions);
    }

    [Fact]
    public void FavoritesLimitAppliesAfterRemovingInvalidIdsAndDuplicates()
    {
        var ids = BibleTextTransitionCatalog.All.Select(preset => preset.Id).ToArray();
        var input = "invalid|" + string.Join('|', ids.SelectMany(id => new[] { id, id }));
        var normalized = new BibleVisualStyle { FavoriteTransitions = input }.Normalize();
        Assert.Equal(ids.Take(64), normalized.FavoriteTransitions.Split('|'));
    }

    [Theory]
    [InlineData("TextPreset:Slide:Words:0")]
    [InlineData("TextPreset:Unroll:Passage:7")]
    public void GeneratedTransitionsSurviveNormalizationAndPersistence(string id)
    {
        var source = new BibleVisualStyle { Transition = id, TransitionMilliseconds = 700 };
        var normalized = source.Normalize();
        Assert.Equal(id, normalized.Transition);
        Assert.True(normalized.UsesTextTransition);
        var restored = JsonSerializer.Deserialize<BibleVisualStyle>(JsonSerializer.Serialize(normalized))!.Normalize();
        Assert.Equal(id, restored.Transition);
        Assert.Equal(700, restored.TransitionMilliseconds);
        Assert.Equal(id, source.Transition);
    }
}
