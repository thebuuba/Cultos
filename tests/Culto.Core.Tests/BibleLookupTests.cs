using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class BibleLookupTests
{
    [Fact]
    public void ARealWordMatchTakesPrecedenceOverAFuzzyBareBookName()
    {
        var catalog = new BibleCatalog(new BibleDocument { Verses =
            [new("Amós", 1, 1, "Profeta sintético"), new("Juan", 3, 16, "Amor sintético")] });
        Assert.Equal("Juan", catalog.Lookup("amor", out var referenceSearch).Single().Book);
        Assert.False(referenceSearch);
        Assert.Equal("Amós", catalog.Lookup("Amós", out referenceSearch).Single().Book);
        Assert.True(referenceSearch);
    }

    private static BibleCatalog Catalog() => new(new BibleDocument
    {
        Verses =
        [
            new("Juan", 3, 16, "Amor sintético"),
            new("Juan", 3, 17, "Segundo sintético"),
            new("Juan", 3, 18, "Tercero sintético"),
            new("Salmos", 23, 1, "Cántico sintético"),
            new("1 Juan", 1, 1, "Carta sintética"),
            new("Hebreos", 5, 1, "Hebreos sintético"),
            new("2 Corintios", 1, 1, "Carta sintética")
        ]
    });

    [Theory]
    [InlineData("Jn 3 16", "Juan", 1)]
    [InlineData("Juan 3:16-18", "Juan", 3)]
    [InlineData("Juan 3", "Juan", 3)]
    [InlineData("Jn", "Juan", 3)]
    [InlineData("Salmo 23", "Salmos", 1)]
    [InlineData("I Jn 1 1", "1 Juan", 1)]
    [InlineData("hebreo 5 1", "Hebreos", 1)]
    [InlineData("hebre 5:1", "Hebreos", 1)]
    [InlineData("hebre", "Hebreos", 1)]
    [InlineData("HEBREO 5", "Hebreos", 1)]
    [InlineData("hebros 5 1", "Hebreos", 1)]
    [InlineData("hebreoz 5 1", "Hebreos", 1)]
    [InlineData("hebreoss 5 1", "Hebreos", 1)]
    [InlineData("heberos 5 1", "Hebreos", 1)]
    [InlineData("2 corinti 1 1", "2 Corintios", 1)]
    public void UnifiedLookupAcceptsNaturalReferencesAndWholeBooks(string query, string book, int count)
    {
        var verses = Catalog().Lookup(query, out var reference);
        Assert.True(reference);
        Assert.Equal(count, verses.Count);
        Assert.All(verses, verse => Assert.Equal(book, verse.Book));
    }

    [Theory]
    [InlineData("Juan 3:")]
    [InlineData("Juan 3:16-")]
    [InlineData("Juan 99")]
    [InlineData("Juan 3 basura")]
    [InlineData("ju 1 1")]
    [InlineData("corinti 1 1")]
    [InlineData("4 Juan 1 1")]
    [InlineData("zzzz 5 1")]
    public void InvalidOrPartialReferenceNeverFallsBackToPhraseSearch(string query) =>
        Assert.Throws<ArgumentException>(() => Catalog().Lookup(query, out _));

    [Fact]
    public void PhraseSearchIsAccentInsensitiveAndPreservesCanonicalVerseObjects()
    {
        var catalog = Catalog();
        var verses = catalog.Lookup("CANTICO SINTETICO", out var reference);
        Assert.False(reference);
        Assert.Same(catalog.Document.Verses[0], Assert.Single(verses));
        Assert.Equal("Salmos", verses[0].Book);
    }

    [Fact]
    public void TolerantSearchDoesNotRelaxImportedBookValidation() =>
        Assert.Throws<InvalidDataException>(() => new BibleCatalog(new BibleDocument
        {
            Verses = [new("hebreo", 5, 1, "Nombre de importación incorrecto")]
        }));
}
