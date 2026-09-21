using Cultos.Core;

namespace Cultos.Tests;

public sealed class CoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cultos-tests-" + Guid.NewGuid());
    private LocalDatabase CreateDb(){var db=new LocalDatabase(Path.Combine(_folder,"cultos.db"));db.Initialize();return db;}

    [Fact] public void CreatesSavesAndReopensService(){var db=CreateDb();var s=new WorshipService{Name="Culto de prueba",Items=[new(){Title="Bienvenida",Content="Hola",Type=ContentType.Welcome}]};db.SaveService(s);var loaded=db.LoadService(s.Id);Assert.Equal("Culto de prueba",loaded!.Name);Assert.Single(loaded.Items);}
    [Fact] public void SearchesBibleReference(){var db=CreateDb();var found=db.SearchBible("Juan 3:16");Assert.Single(found);Assert.Contains("mundo",found[0].Text);}
    [Fact] public void SearchesHymnNumber(){var db=CreateDb();var found=db.SearchHymns("Himno 156");Assert.Single(found);Assert.Equal(156,found[0].Number);}
    [Fact] public void SplitsLongText(){var text=string.Join(' ',Enumerable.Repeat("palabra",50));var slides=TextPaginator.Split(text,80);Assert.True(slides.Count>1);Assert.All(slides,x=>Assert.True(x.Length<=80));}
    [Fact] public void DetectsMissingMedia(){var db=CreateDb();Assert.False(db.MediaExists(new(){MediaPath=Path.Combine(_folder,"missing.mp4")}));}
    [Fact] public void RecoversActiveService(){var db=CreateDb();var s=db.SaveService(new(){Name="Recuperable"});Assert.Equal(s.Id,db.LoadActive()!.Id);}
    [Fact] public void ExportsAndImportsBackup(){var db=CreateDb();var s=db.SaveService(new(){Name="Exportado",Items=[new(){Title="Texto",Content="Contenido"}]});var file=Path.Combine(_folder,"backup.cultos");db.ExportService(s,file);var imported=db.ImportService(file);Assert.NotEqual(s.Id,imported.Id);Assert.Single(imported.Items);}
    public void Dispose(){if(Directory.Exists(_folder))Directory.Delete(_folder,true);}
}
