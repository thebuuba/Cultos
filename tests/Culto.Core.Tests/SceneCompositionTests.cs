using Culto.Core;
using System.Text.Json;
using Xunit;

namespace Culto.Core.Tests;

public sealed class SceneCompositionTests
{
    [Fact]
    public void SceneKindPreservesPreviousNumericValues()
    {
        Assert.True(Enum.TryParse<CueKind>("Scene", out var kind));
        Assert.Equal(8, (int)kind);
        Assert.Equal(7, (int)CueKind.BrowserSource);
    }

    [Fact]
    public void CopiesPreserveSourceIdentityAndIsolateEditsThroughPersistence()
    {
        var cue = new Cue { Kind = CueKind.Scene, Scene = new SceneComposition {
            Layers = [new SceneLayer { Source = new Cue { Kind = CueKind.Text, Body = "Original" } }] } };
        var copy = cue.Copy(true);
        Assert.NotEqual(cue.Id, copy.Id);
        Assert.Equal(cue.Scene.Layers[0].SourceId, copy.Scene!.Layers[0].SourceId);
        copy.Scene.Layers[0].Source.Body = "Edited";
        copy.Scene.Layers[0].X = 100;
        Assert.Equal("Original", cue.Scene.Layers[0].Source.Body);
        Assert.Equal(0, cue.Scene.Layers[0].X);
        var restored = JsonSerializer.Deserialize<Cue>(JsonSerializer.Serialize(cue))!;
        Assert.Equal(cue.Scene.Layers[0].SourceId, restored.Scene!.Layers[0].SourceId);
        Assert.Null(JsonSerializer.Deserialize<Cue>("{\"Kind\":0}")!.Scene);
    }

    [Theory]
    [InlineData(CueKind.Scene)]
    [InlineData(CueKind.PowerPoint)]
    public void NestedScenesAndPowerPointAreRejected(CueKind kind)
    {
        var scene = new SceneComposition { Layers = [new SceneLayer { Source = new Cue { Kind = kind } }] };
        Assert.Throws<ArgumentException>(() => scene.Normalize());
    }

    [Fact]
    public void InvalidGeometryAndExcessLayersAreRejected()
    {
        var scene = new SceneComposition { Layers = [new SceneLayer()] };
        scene.Layers[0].X = double.NaN;
        Assert.Throws<ArgumentException>(() => scene.Validate());
        scene.Layers[0].X = 0;
        scene.Layers[0].Width = 0;
        Assert.Throws<ArgumentException>(() => scene.Validate());
        scene.Layers = Enumerable.Range(0, 33).Select(_ => new SceneLayer()).ToList();
        Assert.Throws<ArgumentException>(() => scene.Validate());
    }

    [Fact]
    public void NormalizationRetainsOrderingAndBoundsAndRepairsMissingIdentity()
    {
        var scene = new SceneComposition { Background = " #aabbcc ", Layers = [
            new SceneLayer { Id = "", SourceId = "", Name = " First ", X = -10, Opacity = .5 },
            new SceneLayer { Name = "Second", Muted = true, Visible = false, Volume = 42 }] };
        var normalized = scene.Normalize();
        normalized.Validate();
        Assert.Equal("#AABBCC", normalized.Background);
        Assert.Equal("First", normalized.Layers[0].Name);
        Assert.False(string.IsNullOrEmpty(normalized.Layers[0].SourceId));
        Assert.Equal("Second", normalized.Layers[1].Name);
        Assert.True(normalized.Layers[1].Muted);
        Assert.False(normalized.Layers[1].Visible);
        Assert.Equal(42, normalized.Layers[1].Volume);
    }

    [Theory]
    [InlineData("X", double.PositiveInfinity)]
    [InlineData("Y", -32769)]
    [InlineData("Width", 16385)]
    [InlineData("Height", -1)]
    [InlineData("Rotation", double.NaN)]
    [InlineData("Opacity", 1.01)]
    [InlineData("Opacity", -0.01)]
    [InlineData("Volume", 101)]
    public void InvalidLayerValuesAreRejected(string property, double value)
    {
        var layer = new SceneLayer();
        typeof(SceneLayer).GetProperty(property)!.SetValue(layer, property == "Volume" ? (object)(int)value : value);
        Assert.Throws<ArgumentException>(() => new SceneComposition { Layers = [layer] }.Normalize());
    }

    [Fact]
    public void SceneCopyRejectsCyclesBeforeRecursing()
    {
        var scene = new SceneComposition();
        var cue = new Cue { Kind = CueKind.Scene, Scene = scene };
        scene.Layers.Add(new SceneLayer { Source = cue });
        Assert.Throws<ArgumentException>(() => cue.Copy());
    }

    [Fact]
    public void CopiesKeepIndependentBrowserOptionsAndSourceIdentitiesAcrossSavedSets()
    {
        var cue = new Cue { Kind = CueKind.Scene, Scene = new SceneComposition { Layers = [new SceneLayer {
            Source = new Cue { Kind = CueKind.BrowserSource, Browser = new BrowserSourceOptions { Location = "https://example.com" } } }] } };
        var saved = SavedSet.Capture("Capas", [cue]);
        var loaded = Assert.Single(saved.CreateItems());
        Assert.Equal(cue.Scene.Layers[0].SourceId, loaded.Scene!.Layers[0].SourceId);
        loaded.Scene.Layers[0].Source.Browser = loaded.Scene.Layers[0].Source.Browser! with { Width = 800 };
        Assert.Equal(1920, cue.Scene.Layers[0].Source.Browser!.Width);
        Assert.Equal(1920, saved.Items[0].Scene!.Layers[0].Source.Browser!.Width);
    }
}
