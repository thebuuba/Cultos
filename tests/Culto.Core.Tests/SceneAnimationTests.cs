using System.Text.Json;
using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class SceneAnimationTests
{
    [Fact]
    public void OldJsonDefaultsAndCopiesIsolateAnimationEdits()
    {
        var original = JsonSerializer.Deserialize<SceneLayer>("{}")!;
        Assert.False(original.Loop);
        Assert.Equal("None", original.Animation.Entrance);
        Assert.Equal(600, original.Animation.DurationMilliseconds);
        var copy = original.Copy();
        Assert.NotSame(original.Animation, copy.Animation);
        copy.Animation.Entrance = "Fade";
        copy.Loop = true;
        Assert.Equal("None", original.Animation.Entrance);
        var restored = JsonSerializer.Deserialize<SceneLayer>(JsonSerializer.Serialize(copy))!;
        Assert.Equal("Fade", restored.Animation.Entrance);
        Assert.True(restored.Loop);
    }

    [Theory]
    [InlineData("Entrance", "Unknown")]
    [InlineData("Exit", "fade")]
    [InlineData("DurationMilliseconds", 99)]
    [InlineData("DurationMilliseconds", 3001)]
    [InlineData("DelayMilliseconds", -1)]
    [InlineData("DelayMilliseconds", 10001)]
    [InlineData("HoldMilliseconds", -1)]
    [InlineData("HoldMilliseconds", 3600001)]
    public void InvalidAnimationIsRejectedAtSceneBoundary(string property, object value)
    {
        var layer = new SceneLayer();
        typeof(SceneAnimationOptions).GetProperty(property)!.SetValue(layer.Animation, value);
        Assert.Throws<ArgumentException>(() => new SceneComposition { Layers = [layer] }.Copy());
    }

    [Fact]
    public void NullAnimationIsRejectedAndBoundaryOptionsAreAccepted()
    {
        Assert.Throws<ArgumentException>(() => new SceneLayer { Animation = null! }.Validate());
        new SceneAnimationOptions { DurationMilliseconds = 100, DelayMilliseconds = 10000, HoldMilliseconds = 3600000 }.Validate();
        new SceneAnimationOptions { DurationMilliseconds = 3000 }.Validate();
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Fade")]
    [InlineData("Rise")]
    [InlineData("SlideLeft")]
    [InlineData("Zoom")]
    public void EffectsRespectDelayEndpointsAndFiniteBounds(string effect)
    {
        var options = new SceneAnimationOptions { Entrance = effect, Exit = effect, DelayMilliseconds = 200 };
        var waiting = SceneAnimationTimeline.SampleEntry(options, 199);
        Assert.Equal(0, waiting.Opacity);
        Assert.False(waiting.Complete);
        Assert.Equal(new SceneAnimationSample(1, 0, 0, 1, true), SceneAnimationTimeline.SampleEntry(options, 800));
        Assert.Equal(new SceneAnimationSample(1, 0, 0, 1, effect == "None"), SceneAnimationTimeline.SampleExit(options, 0));
        Assert.True(SceneAnimationTimeline.SampleExit(options, 600).Complete);
        Assert.Equal(effect == "None" ? 1 : 0, SceneAnimationTimeline.SampleExit(options, 600).Opacity);
        foreach (var elapsed in new[] { -1d, 0, 200, 350, 799, 10000, double.MaxValue })
        {
            var sample = SceneAnimationTimeline.SampleEntry(options, elapsed);
            Assert.InRange(sample.Opacity, 0, 1);
            Assert.InRange(sample.OffsetX, -.12, .12);
            Assert.InRange(sample.OffsetY, -.12, .12);
            Assert.InRange(sample.Scale, .9, 1.08);
        }
    }

    [Fact]
    public void MotionUsesLayerFractionsAndEasesTowardBaseline()
    {
        var rise = new SceneAnimationOptions { Entrance = "Rise", DurationMilliseconds = 1000 };
        Assert.Equal(.12, SceneAnimationTimeline.SampleEntry(rise, 0).OffsetY);
        Assert.InRange(SceneAnimationTimeline.SampleEntry(rise, 500).OffsetY, 0.001, .059);
        Assert.Equal(-.12, SceneAnimationTimeline.SampleEntry(rise with { Entrance = "SlideLeft" }, 0).OffsetX);
        Assert.Equal(.9, SceneAnimationTimeline.SampleEntry(rise with { Entrance = "Zoom" }, 0).Scale);
    }

    [Fact]
    public void HoldStartsAfterEntranceAndSchedulesExitOnlyWhenConfigured()
    {
        var options = new SceneAnimationOptions { Entrance = "Fade", Exit = "Fade", DurationMilliseconds = 600, DelayMilliseconds = 200, HoldMilliseconds = 1000 };
        Assert.Equal(new SceneAnimationSample(1, 0, 0, 1, false), SceneAnimationTimeline.SampleEntry(options, 1799));
        Assert.InRange(SceneAnimationTimeline.SampleEntry(options, 2100).Opacity, .01, .99);
        Assert.Equal(new SceneAnimationSample(0, 0, 0, 1, true), SceneAnimationTimeline.SampleEntry(options, 2400));
        Assert.Equal(new SceneAnimationSample(1, 0, 0, 1, true), SceneAnimationTimeline.SampleEntry(options with { HoldMilliseconds = 0 }, 10000));
        Assert.Equal(new SceneAnimationSample(1, 0, 0, 1, true), SceneAnimationTimeline.SampleEntry(options with { Exit = "None" }, 10000));
        Assert.Equal(new SceneAnimationSample(1, 0, 0, 1, false), SceneAnimationTimeline.SampleEntry(options with { Entrance = "None" }, 1199));
        Assert.Equal(0, SceneAnimationTimeline.SampleEntry(options with { Entrance = "None" }, 1800).Opacity);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteTimeIsRejected(double elapsed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneAnimationTimeline.SampleEntry(new(), elapsed));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneAnimationTimeline.SampleExit(new(), elapsed));
    }

    [Theory]
    [InlineData("welcome")]
    [InlineData("reading")]
    [InlineData("sermon")]
    [InlineData("announcement")]
    public void TemplatesProvideValidIndependentEditableLayersAndStagger(string id)
    {
        Assert.Contains(SceneTemplates.Catalog, t => t.Id == id && t.Name.Length > 0 && t.Description.Length > 0);
        var scene = SceneTemplates.Create(id);
        scene.Validate();
        Assert.True(scene.Layers.Count >= 2);
        Assert.All(scene.Layers, layer => Assert.Equal(CueKind.Text, layer.Source.Kind));
        Assert.All(scene.Layers, layer => Assert.False(string.IsNullOrWhiteSpace(layer.Source.Body)));
        Assert.True(scene.Layers.Select(l => l.Animation.DelayMilliseconds).Distinct().Count() > 1);
        scene.Layers[0].Source.Body = "Edited";
        Assert.NotEqual("Edited", SceneTemplates.Create(id).Layers[0].Source.Body);
    }

    [Theory]
    [InlineData("welcome", new double[] { 400, 610 })]
    [InlineData("reading", new double[] { 230, 515, 795 })]
    [InlineData("sermon", new double[] { 640, 815 })]
    [InlineData("announcement", new double[] { 290, 540, 785 })]
    public void TemplateTextPreservesSourceAspectRatioAndIntendedCenters(string id, double[] centers)
    {
        var layers = SceneTemplates.Create(id).Layers;
        for (var i = 0; i < layers.Count; i++)
        {
            Assert.Equal(16d / 9, layers[i].Width / layers[i].Height, 8);
            Assert.Equal(960, layers[i].X + layers[i].Width / 2);
            Assert.Equal(centers[i], layers[i].Y + layers[i].Height / 2);
        }
        Assert.True(layers[0].Width > layers[1].Width);
    }

    [Theory]
    [InlineData(CueKind.Image)]
    [InlineData(CueKind.Video)]
    public void ChosenBackgroundRetainsSourceIdentityAcrossTemplatesWithoutAliasing(CueKind kind)
    {
        var background = new Cue { Kind = kind, FilePath = "background.mp4" };
        var first = SceneTemplates.Create("welcome", background).Layers[0];
        var second = SceneTemplates.Create("sermon", background).Layers[0];
        Assert.Equal(first.SourceId, second.SourceId);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(background.Id, first.Source.Id);
        Assert.NotSame(background, first.Source);
        Assert.Equal(1920, first.Width);
        Assert.Equal(1080, first.Height);
        if (kind == CueKind.Video) { Assert.True(first.Muted); Assert.True(first.Loop); }
    }

    [Fact]
    public void UnknownTemplatesAndUnsupportedBackgroundsAreRejected()
    {
        Assert.Equal(4, SceneTemplates.Catalog.Count);
        Assert.Throws<ArgumentException>(() => SceneTemplates.Create("missing"));
        Assert.Throws<ArgumentException>(() => SceneTemplates.Create("welcome", new Cue { Kind = CueKind.Audio }));
    }
}
