using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Culto.Core;

public sealed class BibleCatalog
{
    private static readonly string[] Books =
    [
        "Génesis", "Éxodo", "Levítico", "Números", "Deuteronomio", "Josué", "Jueces", "Rut",
        "1 Samuel", "2 Samuel", "1 Reyes", "2 Reyes", "1 Crónicas", "2 Crónicas", "Esdras", "Nehemías",
        "Ester", "Job", "Salmos", "Proverbios", "Eclesiastés", "Cantares", "Isaías", "Jeremías", "Lamentaciones",
        "Ezequiel", "Daniel", "Oseas", "Joel", "Amós", "Abdías", "Jonás", "Miqueas", "Nahúm", "Habacuc", "Sofonías",
        "Hageo", "Zacarías", "Malaquías", "Mateo", "Marcos", "Lucas", "Juan", "Hechos", "Romanos",
        "1 Corintios", "2 Corintios", "Gálatas", "Efesios", "Filipenses", "Colosenses", "1 Tesalonicenses",
        "2 Tesalonicenses", "1 Timoteo", "2 Timoteo", "Tito", "Filemón", "Hebreos", "Santiago", "1 Pedro",
        "2 Pedro", "1 Juan", "2 Juan", "3 Juan", "Judas", "Apocalipsis"
    ];
    private static readonly string[] Abbreviations =
    [
        "gen|gn", "ex|exo", "lev|lv", "num|nm", "deut|dt", "jos", "jue|juec", "rt",
        "1sam|1sa|1sm", "2sam|2sa|2sm", "1re|1r", "2re|2r", "1cron|1cro|1cr", "2cron|2cro|2cr", "esd|ezr", "neh|ne",
        "est", "jb", "sal|sl|salmo", "prov|pr", "ecl|ec", "cant|cnt|canta|cantar|cantar de los cantares", "isa|is", "jer|jr", "lam|lm",
        "eze|ez|ezq", "dan|dn", "os|ose", "jl", "am", "abd|ab", "jon", "miq|mi", "nah|na", "hab", "sof|so",
        "hag|ag|ageo", "zac|zc", "mal|ml", "mat|mt", "mar|mr|mc", "luc|lc", "jn|jua", "hech|hch|hechos de los apostoles", "rom|ro|rm",
        "1cor|1co", "2cor|2co", "gal|ga", "efe|ef", "fil|flp", "col", "1tes|1ts", "2tes|2ts", "1tim|1ti|1tm", "2tim|2ti|2tm",
        "tit", "flm|filem|film", "heb|he", "sant|stg|sgo", "1ped|1pe|1p", "2ped|2pe|2p", "1jn|1jua|1j", "2jn|2jua|2j", "3jn|3jua|3j", "jud|jd", "apoc|ap|apo|revelacion"
    ];
    private static readonly Dictionary<string, int> BookNumbers = CreateBookNumbers();
    private readonly List<BibleVerse> verses;
    private readonly string[] normalizedTexts;
    public BibleDocument Document { get; }

    public BibleCatalog(BibleDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Verses is null || document.Verses.Count == 0)
            throw new InvalidDataException("La Biblia importada no contiene versículos.");
        var seen = new HashSet<(int, int, int)>();
        verses = [];
        foreach (var verse in document.Verses)
        {
            if (verse is null || !TryBook(verse.Book, out var number) || verse.Chapter <= 0 || verse.Verse <= 0 || string.IsNullOrWhiteSpace(verse.Text))
                throw new InvalidDataException("La Biblia contiene un libro, capítulo, versículo o texto inválido.");
            if (!seen.Add((number, verse.Chapter, verse.Verse)))
                throw new InvalidDataException($"Versículo duplicado: {Books[number]} {verse.Chapter}:{verse.Verse}.");
            verses.Add(verse with { Book = Books[number] });
        }
        verses = verses.OrderBy(v => BookNumbers[BookKey(v.Book)]).ThenBy(v => v.Chapter).ThenBy(v => v.Verse).ToList();
        normalizedTexts = verses.Select(verse => Normalize(verse.Text)).ToArray();
        Document = new BibleDocument { Name = document.Name ?? "", Attribution = document.Attribution ?? "", Verses = [.. verses] };
    }

    public static BibleCatalog Load(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(path);
                var document = JsonSerializer.Deserialize<BibleDocument>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new BibleCatalog(document ?? throw new InvalidDataException("El archivo no contiene una Biblia válida."));
            }
            if (!Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Importe una Biblia en formato JSON o Zefania XML.");
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 128 * 1024 * 1024 });
            var root = XDocument.Load(reader).Root;
            if (root?.Name.LocalName != "XMLBIBLE") throw new InvalidDataException("El archivo no es una Biblia Zefania XML.");
            var information = Children(root, "INFORMATION").FirstOrDefault();
            var documentXml = new BibleDocument
            {
                Name = (string?)root.Attribute("biblename") ?? information?.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value ?? "",
                Attribution = information is null ? "" : string.Join(Environment.NewLine, information.Elements().Where(e => !string.IsNullOrWhiteSpace(e.Value)).Select(e => $"{e.Name.LocalName}: {e.Value.Trim()}"))
            };
            foreach (var book in Children(root, "BIBLEBOOK"))
            {
                if (!int.TryParse((string?)book.Attribute("bnumber"), out var number) || number < 1 || number > Books.Length)
                    throw new InvalidDataException("Número de libro Zefania inválido; se admiten los 66 libros canónicos.");
                foreach (var chapter in Children(book, "CHAPTER"))
                {
                    if (!int.TryParse((string?)chapter.Attribute("cnumber"), out var chapterNumber)) throw new InvalidDataException("Capítulo Zefania inválido.");
                    foreach (var verse in Children(chapter, "VERS"))
                    {
                        if (!int.TryParse((string?)verse.Attribute("vnumber"), out var verseNumber)) throw new InvalidDataException("Versículo Zefania inválido.");
                        documentXml.Verses.Add(new BibleVerse(Books[number - 1], chapterNumber, verseNumber, verse.Value.Trim()));
                    }
                }
            }
            return new BibleCatalog(documentXml);
        }
        catch (Exception exception) when (exception is JsonException or XmlException)
        {
            throw new InvalidDataException("No se pudo importar la Biblia: el archivo es inválido o contiene una DTD prohibida.", exception);
        }
    }

    public IReadOnlyList<BibleVerse> FindReference(string query)
    {
        var match = Regex.Match(query?.Trim() ?? "", @"^(?<book>.+?)\s*(?<chapter>\d+)(?:\s*[:.,]\s*|\s+)(?<start>\d+)(?:\s*[-–—]\s*(?<end>\d+))?$", RegexOptions.CultureInvariant);
        if (!match.Success || !TryBook(match.Groups["book"].Value, out var book) ||
            !int.TryParse(match.Groups["chapter"].Value, out var chapter) || chapter <= 0 ||
            !int.TryParse(match.Groups["start"].Value, out var start) || start <= 0)
            throw InvalidReference();
        var end = start;
        if (match.Groups["end"].Success && !int.TryParse(match.Groups["end"].Value, out end)) throw InvalidReference();
        if (end < start) throw InvalidReference();
        var result = verses.Where(v => v.Book == Books[book] && v.Chapter == chapter && v.Verse >= start && v.Verse <= end).ToArray();
        if (result.Length != (long)end - start + 1) throw InvalidReference();
        return result;
    }

    public IReadOnlyList<BibleVerse> FindChapter(string query)
    {
        var match = Regex.Match(query?.Trim() ?? "", @"^(?<book>.+?)\s*(?<chapter>\d+)$", RegexOptions.CultureInvariant);
        if (!match.Success || !TryBook(match.Groups["book"].Value, out var book)
            || !int.TryParse(match.Groups["chapter"].Value, out var chapter) || chapter <= 0)
            throw new ArgumentException("El capítulo bíblico es inválido. Use, por ejemplo, Juan 3.", nameof(query));
        var result = verses.Where(verse => verse.Book == Books[book] && verse.Chapter == chapter).ToArray();
        if (result.Length == 0) throw new ArgumentException("El capítulo no está disponible en la Biblia importada.", nameof(query));
        return result;
    }

    public IReadOnlyList<BibleVerse> Lookup(string query, out bool referenceSearch)
    {
        query = query?.Trim() ?? "";
        referenceSearch = true;
        if (TryBook(query, out var book))
        {
            var first = verses.FirstOrDefault(verse => verse.Book == Books[book]);
            if (first is null) throw InvalidReference();
            return FindChapter($"{first.Book} {first.Chapter}");
        }
        if (Regex.IsMatch(query, @"\p{L}.*\d|[:–—]|\d\s*-"))
        {
            if (Regex.IsMatch(query, @"\d(?:\s*[:.,]\s*|\s+)\d|[:–—]|\d\s*-"))
                return FindReference(query);
            return FindChapter(query);
        }
        referenceSearch = false;
        return Search(query);
    }

    public IReadOnlyList<BibleVerse> Search(string query, int limit = 100)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), "El límite de búsqueda debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(query)) return [];
        var normalized = Normalize(query.Trim());
        return verses.Where((_, index) => normalizedTexts[index].Contains(normalized, StringComparison.Ordinal)).Take(limit).ToArray();
    }

    public Cue ToCue(BibleVerse verse)
    {
        ArgumentNullException.ThrowIfNull(verse);
        var reference = $"{verse.Book} {verse.Chapter}:{verse.Verse}";
        return new Cue { Title = reference, Kind = CueKind.Bible, Body = verse.Text, Reference = reference, Version = Document.Name, Notes = Document.Attribution };
    }

    private static ArgumentException InvalidReference() => new("La referencia bíblica es inválida o está incompleta en la Biblia importada. Use, por ejemplo, Juan 3:16-17.", "query");
    private static IEnumerable<XElement> Children(XElement element, string name) => element.Elements().Where(e => e.Name.LocalName == name);
    private static bool TryBook(string? value, out int number) => BookNumbers.TryGetValue(BookKey(value ?? ""), out number);

    private static Dictionary<string, int> CreateBookNumbers()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < Books.Length; i++)
            foreach (var alias in Abbreviations[i].Split('|').Prepend(Books[i])) result.Add(BookKey(alias), i);
        return result;
    }

    private static string BookKey(string value)
    {
        value = Normalize(value).Replace("ª", "").Replace("º", "").Trim();
        value = Regex.Replace(value, @"^(iii|ii|i|primera|primero|segunda|segundo|tercera|tercero)(?=[\s.])", m => m.Value switch
        { "i" or "primera" or "primero" => "1", "ii" or "segunda" or "segundo" => "2", _ => "3" });
        return string.Concat(value.Where(char.IsLetterOrDigit));
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(char.ToLowerInvariant(character));
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
