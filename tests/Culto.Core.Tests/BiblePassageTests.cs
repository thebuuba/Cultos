using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class BiblePassageTests
{
    private static BibleCatalog Catalog() => new(new BibleDocument
    {
        Name = "Biblia sintética", Attribution = "Textos de prueba",
        Verses =
        [
            new("Génesis", 1, 1, "Primero sintético"),
            new("Génesis", 1, 2, "Segundo sintético"),
            new("Génesis", 1, 3, "Tercero sintético"),
            new("Génesis", 1, 4, "Cuarto sintético"),
            new("Génesis", 1, 5, "Quinto sintético"),
            new("Génesis", 2, 1, "Capítulo siguiente sintético"),
            new("Juan", 3, 16, "Otro libro sintético")
        ]
    });

    private static Cue Combine(BibleCatalog catalog, IEnumerable<BibleVerse> verses) => catalog.ToCue(verses);

    [Fact]
    public void ExplicitRangeBecomesOneNumberedCueWithVersionAndAttribution()
    {
        var catalog = Catalog();
        var cue = Combine(catalog, catalog.FindReference("Gn 1:1-3"));
        Assert.Equal(CueKind.Bible, cue.Kind);
        Assert.Equal("Génesis 1:1-3", cue.Reference);
        Assert.Equal(cue.Reference, cue.Title);
        Assert.Equal("1 Primero sintético\n2 Segundo sintético\n3 Tercero sintético", cue.Body);
        Assert.Equal(catalog.Document.Name, cue.Version);
        Assert.Equal(catalog.Document.Attribution, cue.Notes);
    }

    [Fact]
    public void SelectionIsDeduplicatedAndOrderedWithoutClaimingUnselectedVerses()
    {
        var catalog = Catalog();
        var verses = catalog.Document.Verses;
        var cue = Combine(catalog, [verses[4], verses[1], verses[0], verses[1]]);
        Assert.Equal("Génesis 1:1-2,5", cue.Reference);
        Assert.Equal("1 Primero sintético\n2 Segundo sintético\n5 Quinto sintético", cue.Body);
    }

    [Fact]
    public void SingleVerseKeepsOriginalFormattingAndUsesCatalogText()
    {
        var catalog = Catalog();
        var original = catalog.ToCue(catalog.Document.Verses[0]);
        var cue = Combine(catalog, [new("Gn", 1, 1, "Texto ajeno que no debe proyectarse")]);
        Assert.Equal(original.Reference, cue.Reference);
        Assert.Equal(original.Body, cue.Body);
        Assert.Equal(original.Version, cue.Version);
        Assert.Equal(original.Notes, cue.Notes);
    }

    [Fact]
    public void MultiChapterSelectionLabelsEachLineUnambiguously()
    {
        var catalog = Catalog();
        var cue = Combine(catalog, [catalog.Document.Verses[5], catalog.Document.Verses[0]]);
        Assert.Equal("Génesis 1:1; Génesis 2:1", cue.Reference);
        Assert.Equal("1:1 Primero sintético\n2:1 Capítulo siguiente sintético", cue.Body);
    }

    [Fact]
    public void MultiBookSelectionKeepsEachReferenceAndCanonicalOrder()
    {
        var catalog = Catalog();
        var cue = Combine(catalog, [catalog.Document.Verses[6], catalog.Document.Verses[0]]);
        Assert.Equal("Génesis 1:1; Juan 3:16", cue.Reference);
        Assert.Equal("Génesis 1:1 Primero sintético\nJuan 3:16 Otro libro sintético", cue.Body);
    }

    [Fact]
    public void EmptyOrUnknownSelectionsAreRejectedInsteadOfProjectingPartialPassages()
    {
        var catalog = Catalog();
        Assert.Throws<ArgumentException>(() => Combine(catalog, []));
        Assert.Throws<ArgumentException>(() => Combine(catalog, [catalog.Document.Verses[0], new("Génesis", 1, 99, "No existe")]));
        Assert.Throws<ArgumentException>(() => Combine(catalog, [new("No es un libro", 1, 1, "No existe")]));
        Assert.Throws<ArgumentNullException>(() => Combine(catalog, null!));
        Assert.Throws<ArgumentException>(() => Combine(catalog, [null!]));
    }

    [Fact]
    public void SelectionKeepsEveryVerseWithoutModifyingCatalogOrInputs()
    {
        var catalog = Catalog();
        var selection = catalog.Document.Verses.ToArray();
        var originalText = selection.Select(v => v.Text).ToArray();
        var cue = Combine(catalog, selection);
        Assert.Equal(selection.Length, cue.Body.Split('\n').Length);
        Assert.Equal(originalText, catalog.Document.Verses.Select(v => v.Text));
        Assert.All(originalText, text => Assert.Contains(text, cue.Body));
    }
}
