using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class BibleChapterTests
{
    private static BibleCatalog Catalog() => new(new BibleDocument
    {
        Name = "Navegación sintética",
        Attribution = "Texto sintético de prueba",
        Verses =
        [
            new("Juan", 3, 3, "Tercero sintético"),
            new("Juan", 4, 1, "Otro capítulo sintético"),
            new("Génesis", 1, 1, "Primero sintético"),
            new("Juan", 3, 1, "Primero del capítulo sintético"),
            new("Juan", 3, 2, "Segundo del capítulo sintético"),
            new("2 Tesalonicenses", 2, 1, "Carta sintética"),
            new("Cantares", 1, 1, "Canto sintético")
        ]
    });

    [Theory]
    [InlineData("Juan 3", "Juan", 3)]
    [InlineData("Jn 3", "Juan", 3)]
    [InlineData("jn3", "Juan", 3)]
    [InlineData("GÉNESIS 1", "Génesis", 1)]
    [InlineData("2 Tes. 2", "2 Tesalonicenses", 1)]
    [InlineData("II Tes 2", "2 Tesalonicenses", 1)]
    [InlineData("Cantar de los Cantares 1", "Cantares", 1)]
    public void ChapterLookupRecognizesBooksAndReturnsWholeOrderedChapter(string query, string expectedBook, int expectedCount)
    {
        var chapter = Catalog().FindChapter(query);
        Assert.Equal(expectedCount, chapter.Count);
        Assert.All(chapter, verse => Assert.Equal(expectedBook, verse.Book));
        Assert.Equal(Enumerable.Range(1, expectedCount), chapter.Select(verse => verse.Verse));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Juan")]
    [InlineData("Juan 0")]
    [InlineData("Juan 99")]
    [InlineData("Juan 3:1")]
    [InlineData("Juan 3-4")]
    [InlineData("Juan 3 basura")]
    [InlineData("Libro desconocido 1")]
    public void InvalidOrMissingChapterProducesSpanishError(string query)
    {
        var error = Assert.Throws<ArgumentException>(() => Catalog().FindChapter(query));
        Assert.Contains("capítulo", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FullChapterIsNotTruncatedToThePhraseSearchLimit()
    {
        var catalog = new BibleCatalog(new BibleDocument
        {
            Name = "Capítulo largo sintético",
            Verses = Enumerable.Range(1, 176).Reverse().Select(number => new BibleVerse("Salmos", 119, number, $"Versículo sintético {number}")).ToList()
        });
        var chapter = catalog.FindChapter("Sal 119");
        Assert.Equal(176, chapter.Count);
        Assert.Equal(1, chapter[0].Verse);
        Assert.Equal(176, chapter[^1].Verse);
    }
}
