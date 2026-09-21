using Cultos.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Cultos.App;

public partial class MainWindow : Window
{
    private readonly LocalDatabase _db;
    private WorshipService _service;
    private readonly ObservableCollection<RunRow> _run = [];
    private string _mode = "Bible";
    private string? _currentMediaFolder;
    private ServiceItem? _preview;
    private PresentationSnapshot _live = new(PresentationState.Empty, "Sin contenido", "");
    private OutputWindow? _output;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };

    public MainWindow()
    {
        InitializeComponent();
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cultos");
        _db = new LocalDatabase(Path.Combine(data, "cultos.db")); _db.Initialize();
        _service = _db.LoadActive() ?? CreateDemoService();
        if (_service.Id == 0) _db.SaveService(_service);
        RunList.ItemsSource = _run; RefreshRun(); LoadLibrary(); ServiceNameText.Text = _service.Name;
        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("h:mm tt"); _clock.Start();
        _saveTimer.Tick += (_, _) => SaveNow(); Closed += (_, _) => { SaveNow(); _output?.Close(); };
    }

    private static WorshipService CreateDemoService() => new(){Name="Culto divino · Demostración",Items=[new(){Type=ContentType.Welcome,Title="Bienvenidos a casa",Content="Bienvenidos\nIglesia local",Status="Preparado"},new(){Type=ContentType.Hymn,Title="Himno inicial",Content="Selecciona un himno desde la biblioteca"},new(){Type=ContentType.FreeText,Title="Oración de apertura",Content="Oración de apertura"},new(){Type=ContentType.Bible,Title="Lectura bíblica",Content="Selecciona una lectura desde la biblioteca"},new(){Type=ContentType.FreeText,Title="Predicación",Content="Firmes en la esperanza"}]};
    private void RefreshRun(){_run.Clear();foreach(var i in _service.Items)_run.Add(new(i));ItemCountText.Text=$"{_run.Count} elementos";}
    private void QueueSave(){SaveStateText.Text="Guardando…";_saveTimer.Stop();_saveTimer.Start();}
    private void SaveNow(){_saveTimer.Stop();_db.SaveService(_service);SaveStateText.Text="Guardado";StatusText.Text="Guardado local completado";}
    private void LoadLibrary(){var q=SearchBox.Text??"";if(_mode=="Bible")LibraryList.ItemsSource=_db.SearchBible(q).Select(v=>new LibraryRow(v.Reference,v.Text,v)).ToList();else if(_mode=="Hymn")LibraryList.ItemsSource=_db.SearchHymns(q).Select(h=>new LibraryRow($"{h.Number} · {h.Title}",h.Lyrics,h)).ToList();else if(_mode=="Media")LoadMediaBrowser(q);}
    private ServiceItem? LibrarySelection(){if(LibraryList.SelectedItem is not LibraryRow row)return null;return row.Source switch{BibleVerse v=>new(){Type=ContentType.Bible,Title=v.Reference,Content=v.Text,Status="Preparado"},Hymn h=>new(){Type=ContentType.Hymn,Title=$"{h.Number} · {h.Title}",Content=h.Lyrics,Status="Preparado"},FileSystemEntry f when !f.IsFolder&&File.Exists(f.Path)=>new(){Type=f.Kind=="Imagen"?ContentType.Image:ContentType.Video,Title=f.Name,Content=f.Kind,MediaPath=f.Path,Status="Preparado"},_=>null};}
    private ServiceItem? MissingMedia(){StatusText.Text="Archivo multimedia ausente · vuelve a importarlo";MessageBox.Show("El archivo fue movido o eliminado. Vuelve a importarlo desde su ubicación actual.","Archivo no encontrado",MessageBoxButton.OK,MessageBoxImage.Warning);return null;}
    private void ResetPreviewMedia(){PreviewVideo.Stop();PreviewVideo.Source=null;PreviewVideo.Visibility=Visibility.Collapsed;PreviewImage.Source=null;PreviewImage.Visibility=Visibility.Collapsed;PreviewContent.Visibility=Visibility.Visible;}
    private void ShowPreview(ServiceItem item){_preview=item;PreviewTitle.Text=item.Title;ResetPreviewMedia();if(item.Type==ContentType.Image&&File.Exists(item.MediaPath)){PreviewContent.Visibility=Visibility.Collapsed;PreviewImage.Source=new BitmapImage(new Uri(item.MediaPath!));PreviewImage.Visibility=Visibility.Visible;}else if(item.Type==ContentType.Video&&File.Exists(item.MediaPath)){PreviewContent.Visibility=Visibility.Collapsed;PreviewVideo.Source=new Uri(item.MediaPath!);PreviewVideo.Visibility=Visibility.Visible;PreviewVideo.Play();}else PreviewContent.Text=item.Content;}
    private void SendLive(){if(_preview is null)return;_live=new(PresentationState.Content,_preview.Title,_preview.Content,_preview.MediaPath);_preview.Status="Presentado";LiveTitle.Text="  "+_preview.Title;ShowLiveMedia(_preview);LiveBadge.Visibility=Visibility.Visible;_output?.Render(_live,_preview.Type);RefreshRun();QueueSave();StatusText.Text="Contenido enviado a la congregación";}
    private void ShowLiveMedia(ServiceItem item){LiveVideo.Stop();LiveVideo.Source=null;LiveVideo.Visibility=Visibility.Collapsed;LiveImage.Source=null;LiveImage.Visibility=Visibility.Collapsed;LiveContent.Visibility=Visibility.Visible;if(item.Type==ContentType.Image&&File.Exists(item.MediaPath)){LiveContent.Visibility=Visibility.Collapsed;LiveImage.Source=new BitmapImage(new Uri(item.MediaPath!));LiveImage.Visibility=Visibility.Visible;}else if(item.Type==ContentType.Video&&File.Exists(item.MediaPath)){LiveContent.Visibility=Visibility.Collapsed;LiveVideo.Source=new Uri(item.MediaPath!);LiveVideo.Visibility=Visibility.Visible;LiveVideo.Play();}else LiveContent.Text=item.Content;}
    private void RenderState(PresentationState state){_live=state switch{PresentationState.Black=>new(state,"Pantalla negra",""),PresentationState.Logo=>new(state,"Logotipo","Iglesia local"),_=>new(PresentationState.Empty,"Sin contenido","")};LiveVideo.Stop();LiveVideo.Visibility=Visibility.Collapsed;LiveImage.Visibility=Visibility.Collapsed;LiveContent.Visibility=Visibility.Visible;LiveTitle.Text="  "+_live.Title;LiveContent.Text=state==PresentationState.Black?"Pantalla negra":state==PresentationState.Logo?"Logotipo de la iglesia":"Sin contenido";LiveBadge.Visibility=state==PresentationState.Empty?Visibility.Collapsed:Visibility.Visible;_output?.Render(_live);}
    private void SearchBox_TextChanged(object sender,System.Windows.Controls.TextChangedEventArgs e){if(IsLoaded)LoadLibrary();}
    private void OrderNav_Click(object s,RoutedEventArgs e){var show=OrderPanel.Visibility!=Visibility.Visible;OrderPanel.Visibility=show?Visibility.Visible:Visibility.Collapsed;OrderSplitter.Visibility=show?Visibility.Visible:Visibility.Collapsed;StatusText.Text=show?"Orden del culto visible":"Orden del culto oculto";}\n    private void BibleNav_Click(object s,RoutedEventArgs e){_mode="Bible";MediaToolbar.Visibility=Visibility.Collapsed;LibraryTitle.Text="Biblia · demostración";SearchBox.Text="";LoadLibrary();}
    private void HymnNav_Click(object s,RoutedEventArgs e){_mode="Hymn";MediaToolbar.Visibility=Visibility.Collapsed;LibraryTitle.Text="Himnario · demostración";SearchBox.Text="";LoadLibrary();}
    private void SongsNav_Click(object s,RoutedEventArgs e){LibraryTitle.Text="Canciones";LibraryList.ItemsSource=new[]{new LibraryRow("Biblioteca local","Editor completo pendiente de la siguiente fase",new object())};}
    private void MediaNav_Click(object s,RoutedEventArgs e){_mode="Media";LibraryTitle.Text="Multimedia";MediaToolbar.Visibility=Visibility.Visible;SearchBox.Text="";_currentMediaFolder=null;LoadLibrary();}
    private void LoadMediaBrowser(string query="")
    {
        var entries=new List<FileSystemEntry>();
        if(_currentMediaFolder is null)
        {
            var shortcuts=new[]{("Escritorio",Environment.SpecialFolder.DesktopDirectory),("Documentos",Environment.SpecialFolder.MyDocuments),("Imágenes",Environment.SpecialFolder.MyPictures),("Videos",Environment.SpecialFolder.MyVideos)};
            foreach(var (name,special) in shortcuts){var path=Environment.GetFolderPath(special);if(Directory.Exists(path))entries.Add(new(name,path,true,"Carpeta del sistema"));}
            foreach(var drive in DriveInfo.GetDrives().Where(d=>d.IsReady))entries.Add(new(drive.Name,drive.RootDirectory.FullName,true,$"Unidad · {FormatBytes(drive.AvailableFreeSpace)} libres"));
            MediaPathText.Text="Este equipo";
        }
        else
        {
            MediaPathText.Text=_currentMediaFolder;
            try
            {
                entries.AddRange(Directory.EnumerateDirectories(_currentMediaFolder).Select(p=>new FileSystemEntry(Path.GetFileName(p),p,true,"Carpeta")));
                var supported=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".jpg",".jpeg",".png",".webp",".bmp",".gif",".mp4",".wmv",".avi",".mov",".mkv"};
                entries.AddRange(Directory.EnumerateFiles(_currentMediaFolder).Where(p=>supported.Contains(Path.GetExtension(p))).Select(p=>{var video=new[]{".mp4",".wmv",".avi",".mov",".mkv"}.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase);return new FileSystemEntry(Path.GetFileName(p),p,false,video?"Video":"Imagen",new FileInfo(p).Length);}));
            }
            catch(UnauthorizedAccessException){StatusText.Text="Windows no permite acceder a esta carpeta";}
            catch(IOException ex){StatusText.Text="No se pudo abrir la carpeta · "+ex.Message;}
        }
        if(!string.IsNullOrWhiteSpace(query))entries=entries.Where(e=>e.Name.Contains(query,StringComparison.OrdinalIgnoreCase)).ToList();
        LibraryList.ItemsSource=entries.OrderByDescending(e=>e.IsFolder).ThenBy(e=>e.Name).Select(e=>new LibraryRow(e.IsFolder?"▱  "+e.Name:e.Name,e.IsFolder?e.Kind:$"{e.Kind} · {FormatBytes(e.Size)}",e)).ToList();
    }
    private static string FormatBytes(long bytes)=>bytes switch{>=1073741824=>$"{bytes/1073741824d:0.0} GB",>=1048576=>$"{bytes/1048576d:0.0} MB",>=1024=>$"{bytes/1024d:0} KB",_=>$"{bytes} B"};
    private void MediaUp_Click(object s,RoutedEventArgs e){if(_currentMediaFolder is null)return;var parent=Directory.GetParent(_currentMediaFolder);_currentMediaFolder=parent?.FullName;SearchBox.Text="";LoadMediaBrowser();}
    private void RefreshMedia_Click(object s,RoutedEventArgs e)=>LoadMediaBrowser(SearchBox.Text??"");
    private void LibraryList_SelectionChanged(object s,System.Windows.Controls.SelectionChangedEventArgs e){var item=LibrarySelection();if(item!=null)ShowPreview(item);}
    private void LibraryList_MouseDoubleClick(object s,MouseButtonEventArgs e){if(LibraryList.SelectedItem is LibraryRow row&&row.Source is FileSystemEntry folder&&folder.IsFolder){_currentMediaFolder=folder.Path;SearchBox.Text="";LoadMediaBrowser();return;}var item=LibrarySelection();if(item!=null){ShowPreview(item);SendLive();}}
    private void AddLibraryItem_Click(object s,RoutedEventArgs e){var item=LibrarySelection();if(item==null)return;_service.Items.Add(item);RefreshRun();RunList.SelectedIndex=_run.Count-1;QueueSave();}
    private void RunList_SelectionChanged(object s,System.Windows.Controls.SelectionChangedEventArgs e){if(RunList.SelectedItem is RunRow row)ShowPreview(row.Item);}
    private void AddFreeText_Click(object s,RoutedEventArgs e){var item=new ServiceItem{Type=ContentType.FreeText,Title="Texto libre",Content="Texto libre para editar",Status="Preparado"};_service.Items.Add(item);RefreshRun();RunList.SelectedIndex=_run.Count-1;QueueSave();}
    private void MoveUp_Click(object s,RoutedEventArgs e)=>MoveSelected(-1); private void MoveDown_Click(object s,RoutedEventArgs e)=>MoveSelected(1);
    private void MoveSelected(int delta){var i=RunList.SelectedIndex;if(i<0)return;var next=i+delta;if(next<0||next>=_service.Items.Count)return;(_service.Items[i],_service.Items[next])=(_service.Items[next],_service.Items[i]);RefreshRun();RunList.SelectedIndex=next;QueueSave();}
    private void RemoveItem_Click(object s,RoutedEventArgs e){var i=RunList.SelectedIndex;if(i<0)return;if(MessageBox.Show("¿Quitar este elemento del orden?","Confirmar",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;_service.Items.RemoveAt(i);RefreshRun();QueueSave();}
    private void Previous_Click(object s,RoutedEventArgs e){if(RunList.SelectedIndex>0)RunList.SelectedIndex--;}
    private void Next_Click(object s,RoutedEventArgs e){if(RunList.SelectedIndex<_run.Count-1)RunList.SelectedIndex++;}
    private void SendLive_Click(object s,RoutedEventArgs e)=>SendLive(); private void Black_Click(object s,RoutedEventArgs e)=>RenderState(_live.State==PresentationState.Black?PresentationState.Empty:PresentationState.Black); private void Clear_Click(object s,RoutedEventArgs e)=>RenderState(PresentationState.Empty); private void RemoveText_Click(object s,RoutedEventArgs e){LiveContent.Text="";_output?.Render(new(PresentationState.Content,_live.Title,"",_live.MediaPath));} private void Logo_Click(object s,RoutedEventArgs e)=>RenderState(PresentationState.Logo);
    private void PlayMedia_Click(object s,RoutedEventArgs e){if(PreviewVideo.Visibility==Visibility.Visible)PreviewVideo.Play();if(LiveVideo.Visibility==Visibility.Visible)LiveVideo.Play();_output?.PlayMedia();StatusText.Text="Reproduciendo video";}
    private void PauseMedia_Click(object s,RoutedEventArgs e){PreviewVideo.Pause();LiveVideo.Pause();_output?.PauseMedia();StatusText.Text="Video en pausa";}
    private void StopMedia_Click(object s,RoutedEventArgs e){PreviewVideo.Stop();LiveVideo.Stop();_output?.StopMedia();StatusText.Text="Video detenido";}
    private void OpenDisplay_Click(object s,RoutedEventArgs e)=>OpenDisplay();
    private void OpenDisplay(){_output?.Close();_output=new OutputWindow();var screens=Forms.Screen.AllScreens;var target=screens.FirstOrDefault(x=>!x.Primary)??screens[0];_output.Left=target.Bounds.Left;_output.Top=target.Bounds.Top;_output.Width=target.Bounds.Width;_output.Height=target.Bounds.Height;_output.Show();_output.WindowState=WindowState.Maximized;_output.Render(_live);DisplayStateText.Text=screens.Length>1?"Pantalla externa conectada":"Salida en este monitor";StatusText.Text=$"Salida iniciada en {target.DeviceName}";}
    private void Export_Click(object s,RoutedEventArgs e){SaveNow();var d=new SaveFileDialog{Filter="Copia de Cultos|*.cultos",FileName=_service.Name.Replace(' ','_')+".cultos"};if(d.ShowDialog()==true){_db.ExportService(_service,d.FileName);StatusText.Text="Copia de seguridad exportada";}}
    private void Import_Click(object s,RoutedEventArgs e){var d=new OpenFileDialog{Filter="Copia de Cultos|*.cultos"};if(d.ShowDialog()==true)try{_service=_db.ImportService(d.FileName);ServiceNameText.Text=_service.Name;RefreshRun();StatusText.Text="Copia importada correctamente";}catch(Exception ex){MessageBox.Show("No se pudo importar la copia. "+ex.Message,"Importación fallida",MessageBoxButton.OK,MessageBoxImage.Error);}}
    private void Services_Click(object s,RoutedEventArgs e)=>MessageBox.Show("El culto activo se guarda automáticamente. La lista completa queda pendiente.","Cultos guardados");
    private void Settings_Click(object s,RoutedEventArgs e)=>MessageBox.Show("Funciona sin internet. Datos: AppData\\Local\\Cultos.","Configuración local");
    private void Window_KeyDown(object s,System.Windows.Input.KeyEventArgs e){if(Keyboard.FocusedElement is System.Windows.Controls.TextBox)return;if(e.Key==Key.Right)Next_Click(s,e);else if(e.Key==Key.Left)Previous_Click(s,e);else if(e.Key==Key.Space)SendLive();else if(e.Key==Key.B)Black_Click(s,e);else if(e.Key==Key.C)Clear_Click(s,e);else if(e.Key==Key.F5)OpenDisplay();else if(e.Key==Key.Escape&&_live.State==PresentationState.Black)RenderState(PresentationState.Empty);}
}
public sealed record LibraryRow(string Title,string Subtitle,object Source);
public sealed record FileSystemEntry(string Name,string Path,bool IsFolder,string Kind,long Size=0);
public sealed class RunRow
{
    public RunRow(ServiceItem item) => Item = item;
    public ServiceItem Item { get; }
    public string Title => Item.Title;
    public string Status => Item.Status;
    public string TypeLabel => Item.Type switch { ContentType.Bible=>"Texto bíblico", ContentType.Hymn=>"Himno", ContentType.Image=>"Imagen", ContentType.Video=>"Video", ContentType.Welcome=>"Bienvenida", _=>"Texto libre" };
}
