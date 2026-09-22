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
    [Fact] public void PreservesParagraphBreaksWhenTheyFit(){var slides=TextPaginator.Split("Primera parte.\n\nSegunda parte.",80);Assert.Single(slides);Assert.Contains("\n\n",slides[0]);}
    [Fact] public void UsesCurrentDatabaseSchema(){var db=CreateDb();Assert.Equal(LocalDatabase.CurrentSchemaVersion,db.GetSchemaVersion());}
    [Fact] public void DetectsMissingMedia(){var db=CreateDb();Assert.False(db.MediaExists(new(){MediaPath=Path.Combine(_folder,"missing.mp4")}));}
    [Fact] public void AddsAndFindsMedia(){var db=CreateDb();Directory.CreateDirectory(_folder);var file=Path.Combine(_folder,"anuncio.png");File.WriteAllBytes(file,[1,2,3]);var added=db.AddMedia(file);var found=db.GetMedia("anuncio");Assert.Equal("Image",added.Kind);Assert.Single(found);Assert.True(found[0].Exists);}
    [Fact] public void RecoversActiveService(){var db=CreateDb();var s=db.SaveService(new(){Name="Recuperable"});Assert.Equal(s.Id,db.LoadActive()!.Id);}
    [Fact] public void ExportsAndImportsBackup(){var db=CreateDb();var s=db.SaveService(new(){Name="Exportado",Items=[new(){Title="Texto",Content="Contenido"}]});var file=Path.Combine(_folder,"backup.cultos");db.ExportService(s,file);var imported=db.ImportService(file);Assert.NotEqual(s.Id,imported.Id);Assert.Single(imported.Items);}
    [Fact] public void SavesAndSearchesSong(){var db=CreateDb();db.SaveSong("Canto local","Autor","Letra de prueba","adoración");var found=db.SearchSongs("Canto local");Assert.Single(found);Assert.Equal("Autor",found[0].Author);}
    [Fact] public void ListsSavedServices(){var db=CreateDb();db.SaveService(new(){Name="Culto A"});db.SaveService(new(){Name="Culto B"});var services=db.ListServices();Assert.Contains(services,x=>x.Name=="Culto A");Assert.Contains(services,x=>x.Name=="Culto B");}
    [Fact] public void SeedsDefaultScenes(){var db=CreateDb();var scenes=db.ListScenes();Assert.Contains(scenes,x=>x.Key=="logo");Assert.Contains(scenes,x=>x.Key=="bible");Assert.Contains(scenes,x=>x.Key=="video");Assert.Contains(scenes,x=>x.Key=="black");}
    [Fact] public void PersistsSceneContent(){var db=CreateDb();db.UpdateSceneContent("bible","Juan 3:16","Texto preparado",null);var scene=db.GetScene("bible");Assert.NotNull(scene);Assert.Equal("Juan 3:16",scene!.Title);Assert.Equal("Texto preparado",scene.Content);}
    [Fact] public void SavesAndDeletesCustomScene(){var db=CreateDb();var scene=db.SaveScene(new PresentationScene{Name="Anuncios",Type=SceneType.Custom,Position=20});Assert.True(scene.Id>0);Assert.Contains(db.ListScenes(),x=>x.Id==scene.Id);Assert.True(db.DeleteScene(scene.Id));Assert.DoesNotContain(db.ListScenes(),x=>x.Id==scene.Id);}
    [Fact]
    public void ExportsAndImportsPortableChurchProfile()
    {
        Directory.CreateDirectory(_folder);
        var logo=Path.Combine(_folder,"logo.png");
        var slide=Path.Combine(_folder,"slide.png");
        File.WriteAllBytes(logo,[1,2,3,4]);
        File.WriteAllBytes(slide,[5,6,7,8]);

        var profile=new ChurchProfile{Name="Iglesia de prueba",LogoPath=logo,Location="Ciudad"};
        var logoSettings=new LogoSceneSettings{ImagePaths=[slide],IntervalSeconds=6,Loop=true};
        var scenes=new List<PresentationScene>
        {
            new(){Key="logo",Name="Logo",Type=SceneType.Logo,Position=0,IsBuiltIn=true,SettingsJson=System.Text.Json.JsonSerializer.Serialize(logoSettings)},
            new(){Key="title",Name="Tema",Type=SceneType.Title,Position=1,IsBuiltIn=true,Title="Tema preparado",Content="Contenido"}
        };

        var package=Path.Combine(_folder,"perfil.cultosperfil");
        ChurchProfilePackageService.Export(package,profile,scenes);
        var imported=ChurchProfilePackageService.Import(package,Path.Combine(_folder,"destino"));

        Assert.Equal("Iglesia de prueba",imported.Profile.Name);
        Assert.True(File.Exists(imported.Profile.LogoPath));
        var importedLogo=imported.Scenes.Single(x=>x.Key=="logo");
        var settings=System.Text.Json.JsonSerializer.Deserialize<LogoSceneSettings>(importedLogo.SettingsJson);
        Assert.NotNull(settings);
        Assert.Single(settings!.ImagePaths);
        Assert.True(File.Exists(settings.ImagePaths[0]));
    }

    [Fact]
    public void ImportsSceneProfileWithoutRemovingBuiltInScenes()
    {
        var db=CreateDb();
        db.SaveScene(new PresentationScene{Name="Antigua personalizada",Type=SceneType.Custom,Position=20});

        db.ImportSceneProfile([
            new(){Key="bible",Name="Biblia principal",Type=SceneType.Bible,Position=2,IsBuiltIn=true},
            new(){Key="custom-anuncios",Name="Anuncios",Type=SceneType.Custom,Position=10,IsBuiltIn=false}
        ]);

        var scenes=db.ListScenes();
        Assert.Contains(scenes,x=>x.Key=="bible"&&x.Name=="Biblia principal");
        Assert.Contains(scenes,x=>x.Key=="logo"&&x.IsBuiltIn);
        Assert.Contains(scenes,x=>x.Key=="custom-anuncios");
        Assert.DoesNotContain(scenes,x=>x.Name=="Antigua personalizada");
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ","dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ","dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ","dQw4w9WgXcQ")]
    public void NormalizesYouTubeUrls(string url,string id){Assert.Equal(id,YouTubeUrlHelper.ExtractVideoId(url));Assert.True(YouTubeUrlHelper.TryBuildEmbedUrl(url,true,out var embed));Assert.Contains(id,embed);}
    public void Dispose(){if(Directory.Exists(_folder))Directory.Delete(_folder,true);}
}
