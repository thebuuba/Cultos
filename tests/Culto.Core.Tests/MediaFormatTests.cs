using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class MediaFormatTests
{
    [Theory]
    [InlineData("wma", CueKind.Audio)]
    [InlineData("m4a", CueKind.Audio)]
    [InlineData("aac", CueKind.Audio)]
    [InlineData("aif", CueKind.Audio)]
    [InlineData("aiff", CueKind.Audio)]
    [InlineData("opus", CueKind.Audio)]
    [InlineData("oga", CueKind.Audio)]
    [InlineData("m4b", CueKind.Audio)]
    [InlineData("flv", CueKind.Video)]
    [InlineData("mpg", CueKind.Video)]
    [InlineData("mpeg", CueKind.Video)]
    [InlineData("m4v", CueKind.Video)]
    [InlineData("3gp", CueKind.Video)]
    [InlineData("3g2", CueKind.Video)]
    [InlineData("vob", CueKind.Video)]
    [InlineData("asf", CueKind.Video)]
    [InlineData("m2ts", CueKind.Video)]
    [InlineData("gif", CueKind.Image)]
    [InlineData("tif", CueKind.Image)]
    [InlineData("tiff", CueKind.Image)]
    [InlineData("wdp", CueKind.Image)]
    public void RecognizesMediaFormatsFromWindowsFolders(string extension, CueKind kind)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "." + extension.ToUpperInvariant());
        try
        {
            File.WriteAllBytes(path, []);
            Assert.True(MediaLibrary.IsSupported(path));
            Assert.Equal(kind, MediaLibrary.FromFile(path).Kind);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("script.ts")]
    [InlineData("declaration.mts")]
    [InlineData("track.cda")]
    public void DoesNotTreatSourceCodeOrCdShortcutsAsMedia(string path) => Assert.False(MediaLibrary.IsSupported(path));
}
