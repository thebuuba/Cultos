namespace Cultos.Core;

public static class TextPaginator
{
    public static IReadOnlyList<string> Split(string text, int maxCharacters = 180)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var slides = new List<string>();
        var current = new List<string>();
        var length = 0;
        foreach (var word in words)
        {
            if (current.Count > 0 && length + word.Length + 1 > maxCharacters)
            {
                slides.Add(string.Join(' ', current));
                current.Clear(); length = 0;
            }
            current.Add(word); length += word.Length + 1;
        }
        if (current.Count > 0) slides.Add(string.Join(' ', current));
        return slides;
    }
}
