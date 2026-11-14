using Culto.Core;
using System.Text.Json;
using Xunit;

namespace Culto.Core.Tests;

public sealed class BrowserSourceTests
{
    [Fact]
    public void BrowserKindIsAppendedWithoutChangingExistingKinds()
    {
        Assert.True(Enum.TryParse<CueKind>("BrowserSource", out var browser));
        Assert.Equal(7, (int)browser);
        Assert.Equal(0, (int)CueKind.Text);
        Assert.Equal(5, (int)CueKind.PowerPoint);
        Assert.Equal(6, (int)CueKind.WindowCapture);
    }

    [Fact]
    public void DefaultsAndNormalizationPreserveRenderingOptions()
    {
        var source = new BrowserSourceOptions { Location = "  https://example.com/title?q=one#two  " };
        var normalized = source.Normalize();
        Assert.Equal("https://example.com/title?q=one#two", normalized.GetAddress());
        Assert.Equal(1920, normalized.Width);
        Assert.Equal(1080, normalized.Height);
        Assert.Equal(30, normalized.FramesPerSecond);
        Assert.False(normalized.AudioEnabled);
        Assert.Contains("transparent", normalized.CustomCss);
        Assert.Contains("margin", normalized.CustomCss);
        Assert.Contains("overflow", normalized.CustomCss);
        Assert.StartsWith("  ", source.Location);
        normalized.Validate();
    }

    [Theory]
    [InlineData(63, 1080, 30)]
    [InlineData(4097, 1080, 30)]
    [InlineData(1920, 63, 30)]
    [InlineData(1920, 4097, 30)]
    [InlineData(1920, 1080, 0)]
    [InlineData(1920, 1080, 61)]
    public void InvalidRenderingLimitsAreRejected(int width, int height, int fps)
    {
        var source = new BrowserSourceOptions { Location = "https://example.com", Width = width, Height = height, FramesPerSecond = fps };
        Assert.Throws<ArgumentException>(() => source.Normalize());
        Assert.Throws<ArgumentException>(() => source.Validate());
    }

    [Theory]
    [InlineData(64, 4096, 1)]
    [InlineData(4096, 64, 60)]
    public void InclusiveRenderingLimitsAreAccepted(int width, int height, int fps)
    {
        var source = new BrowserSourceOptions { Location = "http://localhost:8080", Width = width, Height = height, FramesPerSecond = fps };
        Assert.Equal(source, source.Normalize());
    }

    [Theory]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("ftp://example.com/title.html")]
    [InlineData("file:///C:/title.html")]
    [InlineData("javascript:alert(1)")]
    public void UnsupportedRemoteLocationsAreRejected(string location)
    {
        var source = new BrowserSourceOptions { Location = location };
        Assert.Throws<ArgumentException>(() => source.Normalize());
        Assert.Throws<ArgumentException>(() => source.GetAddress());
    }

    [Fact]
    public void LocalHtmlNormalizationDoesNotRequireExistenceButValidationDoes()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + " title #1.HTM");
        var source = new BrowserSourceOptions { Location = path, IsLocalFile = true };
        var normalized = source.Normalize();
        var address = new Uri(normalized.GetAddress());
        Assert.True(address.IsFile);
        Assert.Equal(path, address.LocalPath);
        Assert.Throws<ArgumentException>(() => normalized.Validate());
        try
        {
            File.WriteAllText(path, "<html></html>");
            normalized.Validate();
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("title.txt")]
    [InlineData("https://example.com/title.html")]
    [InlineData("file:///C:/title.html")]
    public void LocalSourceRequiresHtmlFilePath(string location)
    {
        Assert.Throws<ArgumentException>(() => new BrowserSourceOptions { Location = location, IsLocalFile = true }.Normalize());
    }

    [Fact]
    public void BrowserOptionsSurviveCopiesAndJsonWhileEditsRemainIndependent()
    {
        var options = new BrowserSourceOptions { Location = "https://example.com", Width = 800, Height = 600,
            FramesPerSecond = 60, CustomCss = "body { color: red; }", AudioEnabled = true };
        var cue = new Cue { Kind = CueKind.BrowserSource, Browser = options, Title = "Titulo" };
        var session = new PresentationSession();
        session.Prepare(cue); session.GoLive();
        var saved = SavedSet.Capture("Navegador", [cue]);
        var added = Assert.Single(saved.CreateItems());
        Assert.Equal(options, session.Live!.Browser);
        Assert.Equal(options, added.Browser);
        Assert.NotEqual(cue.Id, added.Id);
        Assert.Equal(options, JsonSerializer.Deserialize<SavedSet>(JsonSerializer.Serialize(saved))!.Items[0].Browser);
        Assert.Null(JsonSerializer.Deserialize<Cue>("{\"Kind\":5,\"Title\":\"Anterior\"}")!.Browser);
        cue.Browser = options with { Location = "https://example.com/edited" };
        Assert.False(session.PreviewFollowsLive);
        Assert.Equal(options, session.Live.Browser);
        added.Browser = options with { Width = 900 };
        Assert.Equal(options, saved.Items[0].Browser);
        session.FollowLive();
        Assert.Equal(options, session.Prepared!.Browser);
        Assert.True(session.PreviewFollowsLive);
    }
}
