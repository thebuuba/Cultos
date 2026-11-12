using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Culto.Core;

public enum BibleReferencePart { Book, Chapter, Verses }

public readonly record struct BibleReferenceSegment(BibleReferencePart Part, int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>Editable spans in a citation. Phrase searches are deliberately not segmented.</summary>
public sealed class BibleReferenceInput
{
    public IReadOnlyList<BibleReferenceSegment> Segments { get; }
    public int? Chapter { get; }
    public int? FirstVerse { get; }
    public int? LastVerse { get; }
    public bool HasVerseRange => LastVerse.HasValue;

    private BibleReferenceInput(BibleReferenceSegment[] segments, int? chapter = null, int? firstVerse = null, int? lastVerse = null)
    {
        Segments = Array.AsReadOnly(segments);
        Chapter = chapter; FirstVerse = firstVerse; LastVerse = lastVerse;
    }

    public static bool TryParse(string? text, [NotNullWhen(true)] out BibleReferenceInput? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        if (BibleCatalog.IsKnownBook(trimmed))
        {
            reference = new([new(BibleReferencePart.Book, text.IndexOf(trimmed, StringComparison.Ordinal), trimmed.Length)]);
            return true;
        }
        var match = Regex.Match(text,
            @"^\s*(?<book>.+?)\s*(?<chapter>[0-9]+)(?:(?:\s*[:.,]\s*|\s+)(?<verses>(?<start>[0-9]+)(?:\s*[-–—]\s*(?<end>[0-9]+))?))?\s*$",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        if (!match.Success || !BibleCatalog.IsKnownBook(match.Groups["book"].Value)
            || !int.TryParse(match.Groups["chapter"].Value, out var chapter) || chapter <= 0) return false;
        int? first = null, last = null;
        if (match.Groups["start"].Success)
        {
            if (!int.TryParse(match.Groups["start"].Value, out var start) || start <= 0) return false;
            first = start;
        }
        if (match.Groups["end"].Success)
        {
            if (!int.TryParse(match.Groups["end"].Value, out var end) || end < first) return false;
            last = end;
        }
        static BibleReferenceSegment Span(Group group, BibleReferencePart part) => new(part, group.Index, group.Length);
        var segments = new List<BibleReferenceSegment>
        {
            Span(match.Groups["book"], BibleReferencePart.Book),
            Span(match.Groups["chapter"], BibleReferencePart.Chapter)
        };
        if (match.Groups["verses"].Success) segments.Add(Span(match.Groups["verses"], BibleReferencePart.Verses));
        reference = new(segments.ToArray(), chapter, first, last);
        return true;
    }

    public BibleReferenceSegment SegmentAt(int characterIndex)
    {
        foreach (var segment in Segments)
            if (characterIndex < segment.End) return segment;
        return Segments[^1];
    }
}
