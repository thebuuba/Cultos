using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class CoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CultoCoreTests-" + Guid.NewGuid());
    private string FileAt(string name) { Directory.CreateDirectory(directory); return Path.Combine(directory, name); }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Fact]
    public void PreparingDoesNotChangeLiveAndGoLiveCopiesAllFields()
    {
        var session = new PresentationSession();
        var a = new Cue { Title = "Primero", Body = "Original", Reference = "Jn 1:1", Version = "Sintética", Notes = "Privado", Kind = CueKind.Bible, HymnNumber = 7, FilePath = "archivo" };
        session.Prepare(a);
        Assert.Null(session.Live);
        session.GoLive();
        a.Body = "Editado";
        session.Prepare(new Cue { Title = "Segundo" });
        Assert.Equal("Original", session.Live!.Body);
        Assert.Equal(a.Id, session.Live.Id);
        Assert.Equal("Jn 1:1", session.Live.Reference);
        Assert.Equal("Sintética", session.Live.Version);
        Assert.Equal("Privado", session.Live.Notes);
        Assert.Equal(CueKind.Bible, session.Live.Kind);
        Assert.Equal(7, session.Live.HymnNumber);
        Assert.Equal("archivo", session.Live.FilePath);
        Assert.NotSame(a, session.Live);
    }

    [Fact]
    public void NavigationUsesLiveIdAndRespectsBounds()
    {
        var plan = new ServicePlan { Items = [new() { Title = "A" }, new() { Title = "B" }, new() { Title = "C" }] };
        var session = new PresentationSession();
        Assert.False(session.Move(plan, 1));
        session.Prepare(plan.Items[0]); session.GoLive();
        session.Prepare(plan.Items[2]);
        Assert.True(session.Move(plan, 1));
        Assert.Equal("B", session.Live!.Title);
        Assert.Equal("B", session.Prepared!.Title);
        Assert.True(session.Move(plan, -1));
        Assert.False(session.Move(plan, -1));
        Assert.False(session.Move(plan, 0));
        plan.Items.Clear();
        Assert.False(session.Move(plan, 1));
        Assert.Equal("A", session.Live.Title);
    }

    [Fact]
    public void NewProjectionClearsBlackoutAndBackgroundAndNotifies()
    {
        var session = new PresentationSession();
        var changes = 0;
        session.Changed += () => changes++;
        session.GoLive();
        Assert.Equal(0, changes);
        session.Prepare(new Cue { Body = "Visible" }); session.GoLive();
        session.ToggleBlackout();
        Assert.True(session.IsBlackout);
        session.ToggleBlackout();
        Assert.False(session.IsBlackout);
        session.ShowBackground();
        Assert.True(session.IsTextHidden);
        Assert.Equal("Visible", session.Live!.Body);
        session.ToggleBlackout(); session.GoLive();
        Assert.False(session.IsBlackout);
        Assert.False(session.IsTextHidden);
        Assert.Equal(7, changes);
    }

    private static BibleCatalog Catalog() => new(new BibleDocument {
        Name = "Prueba sintética", Attribution = "Contenido sintético de prueba",
        Verses = [new("Apocalipsis", 1, 1, "Final sintético"), new("Juan", 3, 17, "Tercero sintético"), new("Génesis", 1, 1, "Árbol SINTÉTICO"), new("Juan", 3, 16, "Segundo sintético"), new("1 Corintios", 13, 4, "Cuarto sintético"), new("Cantares", 1, 1, "Canto sintético")]
    });

    [Theory]
    [InlineData("gén. 1:1", "Génesis", 1)]
    [InlineData("GENESIS 1:1", "Génesis", 1)]
    [InlineData("Jn 3:16-17", "Juan", 2)]
    [InlineData("Juan 3:16–17", "Juan", 2)]
    [InlineData("I Cor. 13:4", "1 Corintios", 1)]
    [InlineData("1co 13:4", "1 Corintios", 1)]
    [InlineData("1ª Corintios 13:4", "1 Corintios", 1)]
    [InlineData("Cantar de los Cantares 1:1", "Cantares", 1)]
    public void ReferencesNormalizeAccentsAbbreviationsAndRanges(string reference, string book, int count)
    {
        var verses = Catalog().FindReference(reference);
        Assert.Equal(count, verses.Count);
        Assert.Equal(book, verses[0].Book);
    }

    [Theory]
    [InlineData("Juan 3")]
    [InlineData("Juan 3:16-18")]
    [InlineData("Juan 3:17-16")]
    [InlineData("Juan 3:0")]
    [InlineData("Juan 3:16-4:1")]
    [InlineData("Juan 3:16 basura")]
    [InlineData("Desconocido 1:1")]
    [InlineData("")]
    public void IncompleteOrInvalidReferenceFailsInSpanish(string query)
    {
        var error = Assert.Throws<ArgumentException>(() => Catalog().FindReference(query));
        Assert.Contains("referencia", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SearchUsesCanonicalOrderAndAccentsAndLimit()
    {
        var catalog = Catalog();
        Assert.Equal(new[] { "Génesis", "Cantares", "Juan", "Juan", "1 Corintios", "Apocalipsis" }, catalog.Search("sintetico").Select(v => v.Book));
        Assert.Equal(16, catalog.FindReference("Juan 3:16-17")[0].Verse);
        Assert.Single(catalog.Search("arbol"));
        Assert.Equal(2, catalog.Search("sintético", 2).Count);
        Assert.Empty(catalog.Search(""));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Search("sintético", 0));
        var cue = catalog.ToCue(catalog.FindReference("gen 1:1")[0]);
        Assert.Equal(CueKind.Bible, cue.Kind);
        Assert.Equal("Génesis 1:1", cue.Reference);
        Assert.Equal("Prueba sintética", cue.Version);
        Assert.Equal("Árbol SINTÉTICO", cue.Body);
    }

    [Fact]
    public void ImportsJsonPreservingNameAttributionAndRejectsDuplicates()
    {
        var path = FileAt("bible.json");
        File.WriteAllText(path, """{"name":"Prueba sintética","attribution":"Autor de prueba","verses":[{"book":"Génesis","chapter":1,"verse":1,"text":"Texto sintético"}]}""");
        var catalog = BibleCatalog.Load(path);
        Assert.Equal("Prueba sintética", catalog.Document.Name);
        Assert.Equal("Autor de prueba", catalog.Document.Attribution);
        Assert.Single(catalog.FindReference("Gen 1:1"));
        Assert.Throws<InvalidDataException>(() => new BibleCatalog(new BibleDocument { Verses = [new("Génesis",1,1,"A"),new("gen",1,1,"B")] }));
        Assert.Throws<InvalidDataException>(() => new BibleCatalog(new BibleDocument { Verses = [new("Libro desconocido",1,1,"A")] }));
    }

    [Fact]
    public void ImportsZefaniaNumbersAndMetadataAndRejectsDtd()
    {
        var path = FileAt("bible.xml");
        File.WriteAllText(path, """<XMLBIBLE biblename="Prueba sintética"><INFORMATION><title>Otro título</title><rights>Autor de prueba</rights></INFORMATION><BIBLEBOOK bnumber="43" bname="John"><CHAPTER cnumber="3"><VERS vnumber="16">Texto <STYLE>de prueba</STYLE> sintético</VERS></CHAPTER></BIBLEBOOK></XMLBIBLE>""");
        var catalog = BibleCatalog.Load(path);
        Assert.Equal("Prueba sintética", catalog.Document.Name);
        Assert.Contains("Autor de prueba", catalog.Document.Attribution);
        Assert.Equal("Texto de prueba sintético", Assert.Single(catalog.FindReference("Jn 3:16")).Text);
        File.WriteAllText(path, """<!DOCTYPE XMLBIBLE [<!ENTITY injected "NO">]><XMLBIBLE biblename="Prueba"><BIBLEBOOK bnumber="1"><CHAPTER cnumber="1"><VERS vnumber="1">&injected;</VERS></CHAPTER></BIBLEBOOK></XMLBIBLE>""");
        Assert.Throws<InvalidDataException>(() => BibleCatalog.Load(path));
    }

    [Fact]
    public void EveryZefaniaBookNumberMapsToCanonicalSpanishOrder()
    {
        string[] expected = ["Génesis", "Éxodo", "Levítico", "Números", "Deuteronomio", "Josué", "Jueces", "Rut", "1 Samuel", "2 Samuel", "1 Reyes", "2 Reyes", "1 Crónicas", "2 Crónicas", "Esdras", "Nehemías", "Ester", "Job", "Salmos", "Proverbios", "Eclesiastés", "Cantares", "Isaías", "Jeremías", "Lamentaciones", "Ezequiel", "Daniel", "Oseas", "Joel", "Amós", "Abdías", "Jonás", "Miqueas", "Nahúm", "Habacuc", "Sofonías", "Hageo", "Zacarías", "Malaquías", "Mateo", "Marcos", "Lucas", "Juan", "Hechos", "Romanos", "1 Corintios", "2 Corintios", "Gálatas", "Efesios", "Filipenses", "Colosenses", "1 Tesalonicenses", "2 Tesalonicenses", "1 Timoteo", "2 Timoteo", "Tito", "Filemón", "Hebreos", "Santiago", "1 Pedro", "2 Pedro", "1 Juan", "2 Juan", "3 Juan", "Judas", "Apocalipsis"];
        var path = FileAt("all-books.xml");
        File.WriteAllText(path, "<XMLBIBLE biblename=\"Prueba sintética\">" + string.Concat(Enumerable.Range(1, 66).Reverse().Select(n => $"<BIBLEBOOK bnumber=\"{n}\"><CHAPTER cnumber=\"1\"><VERS vnumber=\"1\">Texto sintético</VERS></CHAPTER></BIBLEBOOK>")) + "</XMLBIBLE>");
        var catalog = BibleCatalog.Load(path);
        Assert.Equal(expected, catalog.Document.Verses.Select(v => v.Book));
        foreach (var book in expected) Assert.Equal(book, Assert.Single(catalog.FindReference(book + " 1:1")).Book);
        Assert.Equal("2 Reyes", Assert.Single(catalog.FindReference("II Re. 1:1")).Book);
        Assert.Equal("3 Juan", Assert.Single(catalog.FindReference("III Jn 1:1")).Book);
        Assert.Equal("2 Timoteo", Assert.Single(catalog.FindReference("Segunda Timoteo 1:1")).Book);
    }

    [Fact]
    public void InvalidImportsDoNotSilentlyDropData()
    {
        var xml = FileAt("duplicate.xml");
        File.WriteAllText(xml, """<XMLBIBLE><BIBLEBOOK bnumber="1"><CHAPTER cnumber="1"><VERS vnumber="1">A sintético</VERS><VERS vnumber="1">B sintético</VERS></CHAPTER></BIBLEBOOK></XMLBIBLE>""");
        Assert.Throws<InvalidDataException>(() => BibleCatalog.Load(xml));
        var json = FileAt("invalid.json");
        File.WriteAllText(json, """{"verses":[{"book":"Génesis","chapter":0,"verse":1,"text":"Sintético"}]}""");
        Assert.Throws<InvalidDataException>(() => BibleCatalog.Load(json));
        File.WriteAllText(json, "{}");
        Assert.Throws<InvalidDataException>(() => BibleCatalog.Load(json));
        File.WriteAllText(json, "{archivo dañado}");
        Assert.Throws<InvalidDataException>(() => BibleCatalog.Load(json));
    }

    [Fact]
    public void PersistenceFailureKeepsLastValidFile()
    {
        var path = FileAt("plan.json");
        JsonFileStore.Save(path, new ServicePlan { Name = "Conservar" });
        Assert.Throws<ArgumentNullException>(() => JsonFileStore.Save<ServicePlan>(path, null!));
        Assert.Equal("Conservar", JsonFileStore.Load<ServicePlan>(path)!.Name);
        File.WriteAllText(path, "archivo dañado");
        Assert.Throws<InvalidDataException>(() => JsonFileStore.Save(path, new ServicePlan { Name = "No perder original" }));
        Assert.Equal("archivo dañado", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Theory]
    [InlineData("001 - Alabanza.MKV", CueKind.Video, 1)]
    [InlineData("Himno 123 - Prueba.mp3", CueKind.Audio, 123)]
    [InlineData("imagen.PNG", CueKind.Image, null)]
    public void MediaValidatesFileAndParsesHymnNumber(string name, CueKind kind, int? number)
    {
        var path = FileAt(name); File.WriteAllText(path, "contenido de prueba");
        var cue = MediaLibrary.FromFile(path);
        Assert.Equal(kind, cue.Kind);
        Assert.Equal(number, cue.HymnNumber);
        Assert.Equal(Path.GetFullPath(path), cue.FilePath);
        Assert.False(string.IsNullOrWhiteSpace(cue.Title));
        Assert.Throws<FileNotFoundException>(() => MediaLibrary.FromFile(FileAt("ausente.mkv")));
        var invalid = FileAt("script.exe"); File.WriteAllText(invalid, "prueba");
        Assert.Throws<ArgumentException>(() => MediaLibrary.FromFile(invalid));
    }

    [Theory]
    [InlineData("mkv")][InlineData("mp4")][InlineData("mov")][InlineData("webm")][InlineData("avi")][InlineData("wmv")]
    [InlineData("mp3")][InlineData("wav")][InlineData("flac")][InlineData("ogg")]
    [InlineData("jpg")][InlineData("jpeg")][InlineData("png")][InlineData("bmp")]
    public void SupportsRequiredMediaFormats(string extension) => Assert.True(MediaLibrary.IsSupported("archivo." + extension));

    [Fact]
    public void PersistenceRoundTripsAndRecoversPreviousAtomicSave()
    {
        var path = FileAt("nested/plan.json");
        Assert.Null(JsonFileStore.Load<ServicePlan>(path));
        JsonFileStore.Save(path, new ServicePlan { Name = "Primero", Items = [new() { Notes = "Privado", HymnNumber = 12 }] });
        JsonFileStore.Save(path, new ServicePlan { Name = "Segundo" });
        Assert.Equal("Segundo", JsonFileStore.Load<ServicePlan>(path)!.Name);
        Assert.True(File.Exists(path + ".bak"));
        File.WriteAllText(path, "JSON corrupto");
        var recovered = JsonFileStore.Load<ServicePlan>(path)!;
        Assert.Equal("Primero", recovered.Name);
        Assert.Equal("Privado", recovered.Items[0].Notes);
        JsonFileStore.Save(path, recovered);
        File.WriteAllText(path, "JSON corrupto otra vez");
        Assert.Equal("Primero", JsonFileStore.Load<ServicePlan>(path)!.Name);
        File.Delete(path);
        Assert.Equal("Primero", JsonFileStore.Load<ServicePlan>(path)!.Name);
        File.WriteAllText(path + ".bak", "también corrupto");
        Assert.Throws<InvalidDataException>(() => JsonFileStore.Load<ServicePlan>(path));
    }

    [Fact]
    public void CorruptOrNullDataNeverSilentlyBecomesEmpty()
    {
        var path = FileAt("plan.json");
        File.WriteAllText(path, "null");
        Assert.Throws<InvalidDataException>(() => JsonFileStore.Load<ServicePlan>(path));
        Assert.Throws<InvalidDataException>(() => JsonFileStore.Save(path, new ServicePlan()));
        Assert.Equal("null", File.ReadAllText(path));
    }
}
