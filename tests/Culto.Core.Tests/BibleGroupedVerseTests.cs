using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class BibleGroupedVerseTests
{
    [Fact]
    public void AGroupedVerseIsFoundFromEitherNumberAndItsTextIsProjectedOnce()
    {
        var catalog = new BibleCatalog(new BibleDocument { Name = "Sintética agrupada", Verses =
            [new("Números", 26, 5, "Grupo sintético") { EndVerse = 6 }, new("Números", 26, 7, "Siguiente sintético")] });
        Assert.Single(catalog.FindReference("Números 26:6"));
        var passage = catalog.FindReference("Números 26:5-7");
        Assert.Equal(2, passage.Count);
        Assert.Equal("Números 26:5-6", catalog.ToCue(passage[0]).Reference);
        var cue = catalog.ToCue(passage);
        Assert.Equal("Números 26:5-7", cue.Reference);
        Assert.Equal("5-6 Grupo sintético\n7 Siguiente sintético", cue.Body);
        Assert.Throws<ArgumentException>(() => catalog.FindReference("Números 26:5-8"));
    }

    [Fact]
    public void GroupedReferencesCannotOverlapOtherVerses()
    {
        Assert.Throws<InvalidDataException>(() => new BibleCatalog(new BibleDocument { Verses =
            [new("Juan", 3, 16, "Grupo") { EndVerse = 17 }, new("Juan", 3, 17, "Solapado")] }));
    }
}
