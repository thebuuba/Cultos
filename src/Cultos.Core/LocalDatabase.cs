using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Cultos.Core;

public sealed class LocalDatabase
{
    public string DatabasePath { get; }
    private string ConnectionString => $"Data Source={DatabasePath};Pooling=False";

    public LocalDatabase(string databasePath)
    {
        DatabasePath = databasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    }

    public void Initialize()
    {
        using var db = new SqliteConnection(ConnectionString); db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS AppSetting(Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Service(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Date TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ServiceItem(Id INTEGER PRIMARY KEY AUTOINCREMENT, ServiceId INTEGER NOT NULL, Type INTEGER NOT NULL, Title TEXT NOT NULL, Content TEXT NOT NULL, MediaPath TEXT, Position INTEGER NOT NULL, Status TEXT NOT NULL, FOREIGN KEY(ServiceId) REFERENCES Service(Id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS BibleVerse(Id INTEGER PRIMARY KEY, Book TEXT NOT NULL, Chapter INTEGER NOT NULL, Verse INTEGER NOT NULL, Text TEXT NOT NULL, Translation TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Hymn(Id INTEGER PRIMARY KEY, Number INTEGER NOT NULL, Title TEXT NOT NULL, Lyrics TEXT NOT NULL, Sections TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Song(Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, Author TEXT NOT NULL, Lyrics TEXT NOT NULL, Tags TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS MediaItem(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Path TEXT NOT NULL, Kind TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Theme(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Background TEXT NOT NULL, Foreground TEXT NOT NULL, FontFamily TEXT NOT NULL);
            """;
        cmd.ExecuteNonQuery();
        Seed(db);
    }

    private static void Seed(SqliteConnection db)
    {
        using var count = db.CreateCommand(); count.CommandText = "SELECT COUNT(*) FROM BibleVerse";
        if ((long)count.ExecuteScalar()! > 0) return;
        using var tx = db.BeginTransaction();
        var verses = new[] {
            new BibleVerse(1,"Juan",3,16,"Porque de tal manera amó Dios al mundo, que ha dado a su Hijo unigénito, para que todo aquel que en él cree, no se pierda, mas tenga vida eterna.","Demostración"),
            new BibleVerse(2,"Salmos",23,1,"Jehová es mi pastor; nada me faltará.","Demostración"),
            new BibleVerse(3,"Filipenses",4,13,"Todo lo puedo en Cristo que me fortalece.","Demostración"),
            new BibleVerse(4,"Apocalipsis",14,12,"Aquí está la paciencia de los santos, los que guardan los mandamientos de Dios y la fe de Jesús.","Demostración") };
        foreach (var v in verses) { using var c=db.CreateCommand(); c.Transaction=tx; c.CommandText="INSERT INTO BibleVerse VALUES($id,$b,$c,$v,$t,$tr)"; c.Parameters.AddWithValue("$id",v.Id);c.Parameters.AddWithValue("$b",v.Book);c.Parameters.AddWithValue("$c",v.Chapter);c.Parameters.AddWithValue("$v",v.Verse);c.Parameters.AddWithValue("$t",v.Text);c.Parameters.AddWithValue("$tr",v.Translation);c.ExecuteNonQuery(); }
        var hymns = new[] { new Hymn(1,156,"Himno de demostración 156","Esta es una letra breve de demostración para probar la presentación.\n\nCoro: Cantemos juntos con esperanza.","Estrofa 1|Coro"), new Hymn(2,77,"Canto de esperanza","Texto demostrativo autorizado para el prototipo.\n\nCoro: Caminamos por la fe.","Estrofa 1|Coro") };
        foreach(var h in hymns){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO Hymn VALUES($id,$n,$t,$l,$s)";c.Parameters.AddWithValue("$id",h.Id);c.Parameters.AddWithValue("$n",h.Number);c.Parameters.AddWithValue("$t",h.Title);c.Parameters.AddWithValue("$l",h.Lyrics);c.Parameters.AddWithValue("$s",h.Sections);c.ExecuteNonQuery();}
        tx.Commit();
    }

    public List<BibleVerse> SearchBible(string query)
    {
        using var db=new SqliteConnection(ConnectionString);db.Open();using var c=db.CreateCommand();
        c.CommandText="SELECT Id,Book,Chapter,Verse,Text,Translation FROM BibleVerse WHERE lower(Book||' '||Chapter||':'||Verse||' '||Text) LIKE $q ORDER BY Book,Chapter,Verse LIMIT 50";c.Parameters.AddWithValue("$q","%"+query.ToLowerInvariant()+"%");
        using var r=c.ExecuteReader();var result=new List<BibleVerse>();while(r.Read())result.Add(new(r.GetInt32(0),r.GetString(1),r.GetInt32(2),r.GetInt32(3),r.GetString(4),r.GetString(5)));return result;
    }

    public List<Hymn> SearchHymns(string query)
    {
        using var db=new SqliteConnection(ConnectionString);db.Open();using var c=db.CreateCommand();
        c.CommandText="SELECT Id,Number,Title,Lyrics,Sections FROM Hymn WHERE lower(Number||' '||Title||' '||Lyrics) LIKE $q ORDER BY Number LIMIT 50";c.Parameters.AddWithValue("$q","%"+query.ToLowerInvariant().Replace("himno","").Trim()+"%");
        using var r=c.ExecuteReader();var result=new List<Hymn>();while(r.Read())result.Add(new(r.GetInt32(0),r.GetInt32(1),r.GetString(2),r.GetString(3),r.GetString(4)));return result;
    }

    public List<LibrarySong> SearchSongs(string query)
    {
        using var db=new SqliteConnection(ConnectionString);db.Open();using var c=db.CreateCommand();
        c.CommandText="SELECT Id,Title,Author,Lyrics,Tags FROM Song WHERE lower(Title||' '||Author||' '||Lyrics||' '||Tags) LIKE $q ORDER BY Title LIMIT 100";
        c.Parameters.AddWithValue("$q","%"+query.ToLowerInvariant()+"%");
        using var r=c.ExecuteReader();var result=new List<LibrarySong>();
        while(r.Read())result.Add(new(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4)));
        return result;
    }

    public LibrarySong SaveSong(string title,string author,string lyrics,string tags="")
    {
        using var db=new SqliteConnection(ConnectionString);db.Open();using var c=db.CreateCommand();
        c.CommandText="INSERT INTO Song(Title,Author,Lyrics,Tags) VALUES($t,$a,$l,$g); SELECT last_insert_rowid();";
        c.Parameters.AddWithValue("$t",title.Trim());c.Parameters.AddWithValue("$a",author.Trim());c.Parameters.AddWithValue("$l",lyrics);c.Parameters.AddWithValue("$g",tags.Trim());
        var id=Convert.ToInt32((long)c.ExecuteScalar()!);
        return new(id,title.Trim(),author.Trim(),lyrics,tags.Trim());
    }

    public List<WorshipService> ListServices()
    {
        using var db=new SqliteConnection(ConnectionString);db.Open();using var c=db.CreateCommand();
        c.CommandText="SELECT Id,Name,Date,UpdatedAt FROM Service ORDER BY UpdatedAt DESC";
        using var r=c.ExecuteReader();var result=new List<WorshipService>();
        while(r.Read())result.Add(new WorshipService{Id=r.GetInt32(0),Name=r.GetString(1),Date=DateTime.Parse(r.GetString(2)),UpdatedAt=DateTime.Parse(r.GetString(3))});
        return result;
    }

    public IReadOnlyList<MediaLibraryItem> GetMedia(string query = "")
    {
        using var db=new SqliteConnection(ConnectionString);db.Open();using var c=db.CreateCommand();
        c.CommandText="SELECT Id,Name,Path,Kind FROM MediaItem WHERE lower(Name||' '||Path) LIKE $q ORDER BY Name";
        c.Parameters.AddWithValue("$q","%"+query.ToLowerInvariant()+"%");
        using var r=c.ExecuteReader();var result=new List<MediaLibraryItem>();
        while(r.Read())result.Add(new(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetString(3)));
        return result;
    }

    public MediaLibraryItem AddMedia(string path)
    {
        var fullPath=Path.GetFullPath(path);
        if(!File.Exists(fullPath))throw new FileNotFoundException("El archivo seleccionado ya no existe.",fullPath);
        var ext=Path.GetExtension(fullPath).ToLowerInvariant();
        var kind=new[]{".jpg",".jpeg",".png",".webp",".bmp",".gif"}.Contains(ext)?"Image":"Video";
        using var db=new SqliteConnection(ConnectionString);db.Open();using var c=db.CreateCommand();
        c.CommandText="SELECT Id,Name,Path,Kind FROM MediaItem WHERE Path=$p LIMIT 1";c.Parameters.AddWithValue("$p",fullPath);
        using(var r=c.ExecuteReader())if(r.Read())return new(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetString(3));
        c.Parameters.Clear();c.CommandText="INSERT INTO MediaItem(Name,Path,Kind) VALUES($n,$p,$k); SELECT last_insert_rowid();";
        c.Parameters.AddWithValue("$n",Path.GetFileName(fullPath));c.Parameters.AddWithValue("$p",fullPath);c.Parameters.AddWithValue("$k",kind);
        var id=Convert.ToInt32((long)c.ExecuteScalar()!);return new(id,Path.GetFileName(fullPath),fullPath,kind);
    }

    public WorshipService SaveService(WorshipService service)
    {
        using var db=new SqliteConnection(ConnectionString);db.Open();using var tx=db.BeginTransaction(); service.UpdatedAt=DateTime.Now;
        using(var c=db.CreateCommand()){c.Transaction=tx;if(service.Id==0){c.CommandText="INSERT INTO Service(Name,Date,UpdatedAt) VALUES($n,$d,$u); SELECT last_insert_rowid();";}else{c.CommandText="UPDATE Service SET Name=$n,Date=$d,UpdatedAt=$u WHERE Id=$id; SELECT $id;";c.Parameters.AddWithValue("$id",service.Id);}c.Parameters.AddWithValue("$n",service.Name);c.Parameters.AddWithValue("$d",service.Date.ToString("O"));c.Parameters.AddWithValue("$u",service.UpdatedAt.ToString("O"));service.Id=Convert.ToInt32((long)c.ExecuteScalar()!);}
        using(var d=db.CreateCommand()){d.Transaction=tx;d.CommandText="DELETE FROM ServiceItem WHERE ServiceId=$id";d.Parameters.AddWithValue("$id",service.Id);d.ExecuteNonQuery();}
        for(var i=0;i<service.Items.Count;i++){var item=service.Items[i];item.ServiceId=service.Id;item.Position=i;using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO ServiceItem(ServiceId,Type,Title,Content,MediaPath,Position,Status) VALUES($s,$t,$n,$c,$m,$p,$st)";c.Parameters.AddWithValue("$s",service.Id);c.Parameters.AddWithValue("$t",(int)item.Type);c.Parameters.AddWithValue("$n",item.Title);c.Parameters.AddWithValue("$c",item.Content);c.Parameters.AddWithValue("$m",(object?)item.MediaPath??DBNull.Value);c.Parameters.AddWithValue("$p",i);c.Parameters.AddWithValue("$st",item.Status);c.ExecuteNonQuery();}
        SetSetting(db,tx,"ActiveServiceId",service.Id.ToString());tx.Commit();return service;
    }

    public WorshipService? LoadService(int id)
    {
        using var db=new SqliteConnection(ConnectionString);db.Open();using var c=db.CreateCommand();c.CommandText="SELECT Id,Name,Date,UpdatedAt FROM Service WHERE Id=$id";c.Parameters.AddWithValue("$id",id);using var r=c.ExecuteReader();if(!r.Read())return null;var s=new WorshipService{Id=r.GetInt32(0),Name=r.GetString(1),Date=DateTime.Parse(r.GetString(2)),UpdatedAt=DateTime.Parse(r.GetString(3))};r.Close();using var i=db.CreateCommand();i.CommandText="SELECT Id,Type,Title,Content,MediaPath,Position,Status FROM ServiceItem WHERE ServiceId=$id ORDER BY Position";i.Parameters.AddWithValue("$id",id);using var ir=i.ExecuteReader();while(ir.Read())s.Items.Add(new(){Id=ir.GetInt32(0),ServiceId=id,Type=(ContentType)ir.GetInt32(1),Title=ir.GetString(2),Content=ir.GetString(3),MediaPath=ir.IsDBNull(4)?null:ir.GetString(4),Position=ir.GetInt32(5),Status=ir.GetString(6)});return s;
    }

    public WorshipService? LoadActive(){using var db=new SqliteConnection(ConnectionString);db.Open();using var c=db.CreateCommand();c.CommandText="SELECT Value FROM AppSetting WHERE Key='ActiveServiceId'";var value=c.ExecuteScalar()?.ToString();return int.TryParse(value,out var id)?LoadService(id):null;}
    public bool MediaExists(ServiceItem item)=>string.IsNullOrWhiteSpace(item.MediaPath)||File.Exists(item.MediaPath);
    public void ExportService(WorshipService service,string path)=>File.WriteAllText(path,JsonSerializer.Serialize(service,new JsonSerializerOptions{WriteIndented=true}));
    public WorshipService ImportService(string path){var service=JsonSerializer.Deserialize<WorshipService>(File.ReadAllText(path))??throw new InvalidDataException("La copia no contiene un culto válido.");service.Id=0;foreach(var i in service.Items)i.Id=0;return SaveService(service);}
    private static void SetSetting(SqliteConnection db,SqliteTransaction tx,string key,string value){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO AppSetting(Key,Value) VALUES($k,$v) ON CONFLICT(Key) DO UPDATE SET Value=$v";c.Parameters.AddWithValue("$k",key);c.Parameters.AddWithValue("$v",value);c.ExecuteNonQuery();}
}
