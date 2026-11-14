using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class BibleReferenceInputTests
{
    [Theory]
    [InlineData("Génesis 1:1", "Génesis", "1", "1", false)]
    [InlineData("  1 Corintios 13:4-7  ", "1 Corintios", "13", "4-7", true)]
    [InlineData("Jn3:16", "Jn", "3", "16", false)]
    [InlineData("hebreo 5 1", "hebreo", "5", "1", false)]
    [InlineData("heberos 5:1-2", "heberos", "5", "1-2", true)]
    [InlineData("1co13:4 - 7", "1co", "13", "4 - 7", true)]
    [InlineData("I Jn 1 1", "I Jn", "1", "1", false)]
    [InlineData("Cantar de los cantares 2:1–3", "Cantar de los cantares", "2", "1–3", true)]
    [InlineData("Sal 23.1—2", "Sal", "23", "1—2", true)]
    public void ReferenceSegmentsPreserveExactEditableText(string text, string book, string chapter, string verses, bool range)
    {
        Assert.True(BibleReferenceInput.TryParse(text, out var input));
        Assert.Equal(new[] { book, chapter, verses }, input!.Segments.Select(s => text.Substring(s.Start, s.Length)));
        Assert.Equal(new[] { BibleReferencePart.Book, BibleReferencePart.Chapter, BibleReferencePart.Verses }, input.Segments.Select(s => s.Part));
        Assert.Equal(range, input.HasVerseRange);
    }

    [Theory]
    [InlineData("Génesis", 1)]
    [InlineData("1 Juan", 1)]
    [InlineData("Juan 3", 2)]
    [InlineData("  Sal 23 ", 2)]
    public void BookAndChapterOnlyInputsHaveOnlyTheirActualSegments(string text, int count)
    {
        Assert.True(BibleReferenceInput.TryParse(text, out var input));
        Assert.Equal(count, input!.Segments.Count);
        Assert.False(input.HasVerseRange);
        Assert.Null(input.FirstVerse);
    }

    [Theory]
    [InlineData("amor eterno")]
    [InlineData("el amor 3:16")]
    [InlineData("Juan 3:")]
    [InlineData("Juan 3:16-")]
    [InlineData("Juan 3:18-16")]
    [InlineData("Juan 0:1")]
    [InlineData("Juan 3:0")]
    [InlineData("Juan 9999999999999999999:1")]
    [InlineData("")]
    public void PhrasesAndIncompleteInvalidCitationsKeepNormalTextEditing(string text) =>
        Assert.False(BibleReferenceInput.TryParse(text, out _));

    [Fact]
    public void SegmentHitTestingAssignsSeparatorsAndTrailingSpaceWithoutLosingBookNumber()
    {
        const string text = "  1 Juan 3:16-18  ";
        Assert.True(BibleReferenceInput.TryParse(text, out var input));
        Assert.Equal(BibleReferencePart.Book, input!.SegmentAt(0).Part);
        Assert.Equal(BibleReferencePart.Book, input.SegmentAt(3).Part);
        Assert.Equal(BibleReferencePart.Chapter, input.SegmentAt(8).Part);
        Assert.Equal(BibleReferencePart.Verses, input.SegmentAt(text.IndexOf(':')).Part);
        Assert.Equal(BibleReferencePart.Verses, input.SegmentAt(text.Length).Part);
        Assert.Equal(3, input.Chapter);
        Assert.Equal(16, input.FirstVerse);
        Assert.Equal(18, input.LastVerse);
    }
}
