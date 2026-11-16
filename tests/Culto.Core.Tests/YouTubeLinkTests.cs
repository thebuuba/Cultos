using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class YouTubeLinkTests
{
    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=tracking", 0)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=1m23s&list=playlist", 83)]
    [InlineData("https://m.youtube.com/shorts/dQw4w9WgXcQ", 0)]
    [InlineData("https://youtube.com/live/dQw4w9WgXcQ?start=42", 42)]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", 0)]
    public void RecognizesVideoUrlsAndKeepsOnlyVideoAndStart(string url, int start)
    {
        Assert.True(YouTubeLink.TryParse(url, out var link));
        Assert.Equal("dQw4w9WgXcQ", link!.VideoId);
        Assert.Equal(start, link.StartSeconds);
        Assert.DoesNotContain("tracking", link.Url);
    }
    [Theory]
    [InlineData("https://youtube.com.evil.test/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com@evil.test/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/playlist?list=anything")]
    [InlineData("https://youtube.com/watch?v=bad")]
    [InlineData("file:///youtube.com/watch?v=dQw4w9WgXcQ")]
    public void RejectsNonVideoAndUntrustedUrls(string url) => Assert.False(YouTubeLink.TryParse(url, out _));
    [Fact]
    public void VideoLinkSurvivesCopyAndPlanPersistence()
    {
        var cue = new Cue { Kind = CueKind.WindowCapture, YouTubeUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" };
        Assert.Equal(cue.YouTubeUrl, cue.Copy(true).YouTubeUrl);
        var restored = System.Text.Json.JsonSerializer.Deserialize<Cue>(System.Text.Json.JsonSerializer.Serialize(cue));
        Assert.Equal(cue.YouTubeUrl, restored!.YouTubeUrl);
    }
}
