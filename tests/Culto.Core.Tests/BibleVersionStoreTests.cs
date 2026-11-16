using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class BibleVersionStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CultoBibleVersions-" + Guid.NewGuid().ToString("N"));
    private static BibleCatalog Catalog(string name, string text) => new(new BibleDocument
    {
        Name = name, Attribution = "Synthetic test data", Verses = [new("Juan", 3, 16, text)]
    });

    [Fact]
    public void VersionsCoexistAndReimportUpdatesOnlyTheSameVersion()
    {
        var store = new BibleVersionStore(directory);
        store.Add(Catalog("Primera", "Texto primero"));
        var second = store.Add(Catalog("Segunda", "Texto segundo"));
        store.Add(Catalog("Primera", "Texto primero actualizado"));
        var reopened = new BibleVersionStore(directory);
        Assert.Equal(new[] { "Primera", "Segunda" }, reopened.List().Select(item => item.Name));
        Assert.Equal("Texto segundo", reopened.Load(second).FindReference("Juan 3:16").Single().Text);
        Assert.Equal("Texto primero actualizado", reopened.Load(reopened.List()[0]).FindReference("Juan 3:16").Single().Text);
    }

    [Fact]
    public void DisplayNamesCannotWriteOutsideTheLibraryAndBadFilesDoNotHideGoodVersions()
    {
        var store = new BibleVersionStore(directory);
        var version = store.Add(Catalog("../../Nombre de versión", "Texto sintético"));
        Assert.Equal(Path.GetFullPath(Path.Combine(directory, "Biblias")), Path.GetDirectoryName(version.FilePath));
        File.WriteAllText(Path.Combine(directory, "Biblias", "broken.json"), "not json");
        Assert.Single(store.List());
        Assert.Single(store.Warnings);
        Assert.Equal("Texto sintético", store.Load(version).Document.Verses.Single().Text);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
