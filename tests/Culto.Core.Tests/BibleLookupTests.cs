using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class BibleLookupTests
{
    private static BibleCatalog Catalog() => new(new BibleDocument
    {
        Verses =
        [
            new("Juan", 3, 16, "Amor sintético"),
            new("Juan", 3, 17, "Segundo sintético"),
            new("Juan", 3, 18, "Tercero sintético"),
            new("Salmos", 23, 1, "Cántico sintético"),
            new("1 Juan", 1, 1, "Carta sintética")
        ]
    });

    [Theory]
    [InlineData("Jn 3 16", "Juan", 1)]
    [InlineData("Juan 3:16-18", "Juan", 3)]
    [InlineData("Juan 3", "Juan", 3)]
    [InlineData("Jn", "Juan", 3)]
    [InlineData("Salmo 23", "Salmos", 1)]
    [InlineData("I Jn 1 1", "1 Juan", 1)]
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
}
