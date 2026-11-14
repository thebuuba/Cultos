using System.Reflection;
using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class CueCopyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Copy_preserves_content_and_detaches_nested_options(bool newId)
    {
        var original = new Cue { Title = "Himno", Kind = CueKind.Video, Body = "Texto", Reference = "Ref", Version = "V",
            FilePath = "video.mp4", HymnNumber = 37, Notes = "Notas", Capture = new WindowCaptureOptions(), Browser = new BrowserSourceOptions() };
        var method = typeof(Cue).GetMethod("Copy", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        var copy = Assert.IsType<Cue>(method.Invoke(original, [newId]));
        Assert.NotSame(original, copy);
        Assert.Equal(newId, original.Id != copy.Id);
        foreach (var property in typeof(Cue).GetProperties().Where(p => p.Name != nameof(Cue.Id)))
            Assert.Equal(property.GetValue(original), property.GetValue(copy));
        Assert.NotSame(original.Capture, copy.Capture);
        Assert.NotSame(original.Browser, copy.Browser);
        copy.Title = "Otro";
        Assert.Equal("Himno", original.Title);
    }
}
