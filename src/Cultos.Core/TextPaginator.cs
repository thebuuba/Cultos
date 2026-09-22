namespace Cultos.Core;

public static class TextPaginator
{
    public static IReadOnlyList<string> Split(string text, int maxCharacters = 180)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        if (maxCharacters < 20) throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        var normalized = text.Replace("\r\n", "\n").Trim();
        var paragraphs = normalized
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var slides = new List<string>();
        var current = "";

        foreach (var paragraph in paragraphs)
        {
            var paragraphParts = SplitParagraph(paragraph, maxCharacters);
            foreach (var part in paragraphParts)
            {
                var candidate = string.IsNullOrWhiteSpace(current)
                    ? part
                    : current + "\n\n" + part;

                if (candidate.Length <= maxCharacters)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(current)) slides.Add(current.Trim());
                current = part;
            }
        }

        if (!string.IsNullOrWhiteSpace(current)) slides.Add(current.Trim());
        return slides;
    }

    private static IReadOnlyList<string> SplitParagraph(string paragraph, int maxCharacters)
    {
        var words = paragraph
            .Replace("\n", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var parts = new List<string>();
        var current = new List<string>();
        var length = 0;

        foreach (var word in words)
        {
            if (current.Count > 0 && length + word.Length + 1 > maxCharacters)
            {
                parts.Add(string.Join(' ', current));
                current.Clear();
                length = 0;
            }

            if (word.Length > maxCharacters)
            {
                if (current.Count > 0)
                {
                    parts.Add(string.Join(' ', current));
                    current.Clear();
                    length = 0;
                }

                for (var offset = 0; offset < word.Length; offset += maxCharacters)
                    parts.Add(word.Substring(offset, Math.Min(maxCharacters, word.Length - offset)));

                continue;
            }

            current.Add(word);
            length += word.Length + (current.Count > 1 ? 1 : 0);
        }

        if (current.Count > 0) parts.Add(string.Join(' ', current));
        return parts;
    }
}
