using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class SavedSetTests
{
    [Fact]
    public void SavedSetCapturesIndependentCuesAndEachUseGetsNewIds()
    {
        var source = new Cue { Title = "Himno", Kind = CueKind.Video, FilePath = "video.mp4", Notes = "Nota",
            Body = "Texto", Reference = "Referencia", Version = "Versión", HymnNumber = 12 };
        var set = SavedSet.Capture("  Alabanza  ", [source]);
        source.Title = "Modificado";
        var first = Assert.Single(set.CreateItems());
        var second = Assert.Single(set.CreateItems());
        Assert.Equal("Alabanza", set.Name);
        Assert.Equal("Himno", first.Title);
        Assert.Equal(CueKind.Video, first.Kind);
        Assert.Equal("video.mp4", first.FilePath);
        Assert.Equal("Nota", first.Notes);
        Assert.Equal("Texto", first.Body);
        Assert.Equal("Referencia", first.Reference);
        Assert.Equal("Versión", first.Version);
        Assert.Equal(12, first.HymnNumber);
        Assert.NotEqual(source.Id, set.Items[0].Id);
        Assert.NotEqual(set.Items[0].Id, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        first.Notes = "Cambió";
        Assert.Equal("Nota", set.Items[0].Notes);
    }

    [Fact]
    public void SavedSetRoundTripKeepsSequenceAndNotes()
    {
        var path = Path.Combine(Path.GetTempPath(), "CultoSavedSet-" + Guid.NewGuid() + ".json");
        try
        {
            var set = SavedSet.Capture("Culto guardado", [new Cue { Title = "Bienvenida", Notes = "Esperar" },
                new Cue { Title = "Lectura", Kind = CueKind.Bible, Body = "Pasaje" }]);
            JsonFileStore.Save(path, set);
            var loaded = JsonFileStore.Load<SavedSet>(path)!;
            Assert.Equal(set.Id, loaded.Id);
            Assert.Equal(new[] { "Bienvenida", "Lectura" }, loaded.CreateItems().Select(c => c.Title));
            Assert.Equal("Esperar", loaded.Items[0].Notes);
            Assert.Equal(CueKind.Bible, loaded.Items[1].Kind);
        }
        finally { File.Delete(path); }
    }
}
