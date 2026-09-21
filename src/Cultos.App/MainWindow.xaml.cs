using Cultos.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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
    private void LoadLibrary(){var q=SearchBox.Text??"";if(_mode=="Bible")LibraryList.ItemsSource=_db.SearchBible(q).Select(v=>new LibraryRow(v.Reference,v.Text,v)).ToList();else if(_mode=="Hymn")LibraryList.ItemsSource=_db.SearchHymns(q).Select(h=>new LibraryRow($"{h.Number} · {h.Title}",h.Lyrics,h)).ToList();}
    private ServiceItem? LibrarySelection(){if(LibraryList.SelectedItem is not LibraryRow row)return null;return row.Source switch{BibleVerse v=>new(){Type=ContentType.Bible,Title=v.Reference,Content=v.Text,Status="Preparado"},Hymn h=>new(){Type=ContentType.Hymn,Title=$"{h.Number} · {h.Title}",Content=h.Lyrics,Status="Preparado"},_=>null};}
    private void ShowPreview(ServiceItem item){_preview=item;PreviewTitle.Text=item.Title;PreviewContent.Text=item.Content;}
    private void SendLive(){if(_preview is null)return;_live=new(PresentationState.Content,_preview.Title,_preview.Content,_preview.MediaPath);_preview.Status="Presentado";LiveTitle.Text="  "+_preview.Title;LiveContent.Text=_preview.Content;LiveBadge.Visibility=Visibility.Visible;_output?.Render(_live);RefreshRun();QueueSave();StatusText.Text="Contenido enviado a la congregación";}
    private void RenderState(PresentationState state){_live=state switch{PresentationState.Black=>new(state,"Pantalla negra",""),PresentationState.Logo=>new(state,"Logotipo","Iglesia local"),_=>new(PresentationState.Empty,"Sin contenido","")};LiveTitle.Text="  "+_live.Title;LiveContent.Text=state==PresentationState.Black?"Pantalla negra":state==PresentationState.Logo?"Logotipo de la iglesia":"Sin contenido";LiveBadge.Visibility=state==PresentationState.Empty?Visibility.Collapsed:Visibility.Visible;_output?.Render(_live);}
    private void SearchBox_TextChanged(object sender,System.Windows.Controls.TextChangedEventArgs e){if(IsLoaded)LoadLibrary();}
    private void BibleNav_Click(object s,RoutedEventArgs e){_mode="Bible";LibraryTitle.Text="Biblia · demostración";SearchBox.Text="";LoadLibrary();}
    private void HymnNav_Click(object s,RoutedEventArgs e){_mode="Hymn";LibraryTitle.Text="Himnario · demostración";SearchBox.Text="";LoadLibrary();}
    private void SongsNav_Click(object s,RoutedEventArgs e){LibraryTitle.Text="Canciones";LibraryList.ItemsSource=new[]{new LibraryRow("Biblioteca local","Editor completo pendiente de la siguiente fase",new object())};}
    private void MediaNav_Click(object s,RoutedEventArgs e){var d=new OpenFileDialog{Title="Importar imagen o video",Filter="Multimedia|*.jpg;*.jpeg;*.png;*.webp;*.mp4;*.wmv;*.avi|Todos|*.*"};if(d.ShowDialog()==true){var item=new ServiceItem{Type=ContentType.Image,Title=Path.GetFileName(d.FileName),Content="Archivo multimedia",MediaPath=d.FileName,Status="Preparado"};_service.Items.Add(item);RefreshRun();ShowPreview(item);QueueSave();}}
    private void LibraryList_SelectionChanged(object s,System.Windows.Controls.SelectionChangedEventArgs e){var item=LibrarySelection();if(item!=null)ShowPreview(item);}
    private void LibraryList_MouseDoubleClick(object s,MouseButtonEventArgs e){var item=LibrarySelection();if(item!=null){ShowPreview(item);SendLive();}}
    private void AddLibraryItem_Click(object s,RoutedEventArgs e){var item=LibrarySelection();if(item==null)return;_service.Items.Add(item);RefreshRun();RunList.SelectedIndex=_run.Count-1;QueueSave();}
    private void RunList_SelectionChanged(object s,System.Windows.Controls.SelectionChangedEventArgs e){if(RunList.SelectedItem is RunRow row)ShowPreview(row.Item);}
    private void AddFreeText_Click(object s,RoutedEventArgs e){var item=new ServiceItem{Type=ContentType.FreeText,Title="Texto libre",Content="Texto libre para editar",Status="Preparado"};_service.Items.Add(item);RefreshRun();RunList.SelectedIndex=_run.Count-1;QueueSave();}
    private void MoveUp_Click(object s,RoutedEventArgs e)=>MoveSelected(-1); private void MoveDown_Click(object s,RoutedEventArgs e)=>MoveSelected(1);
    private void MoveSelected(int delta){var i=RunList.SelectedIndex;if(i<0)return;var next=i+delta;if(next<0||next>=_service.Items.Count)return;(_service.Items[i],_service.Items[next])=(_service.Items[next],_service.Items[i]);RefreshRun();RunList.SelectedIndex=next;QueueSave();}
    private void RemoveItem_Click(object s,RoutedEventArgs e){var i=RunList.SelectedIndex;if(i<0)return;if(MessageBox.Show("¿Quitar este elemento del orden?","Confirmar",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;_service.Items.RemoveAt(i);RefreshRun();QueueSave();}
    private void Previous_Click(object s,RoutedEventArgs e){if(RunList.SelectedIndex>0)RunList.SelectedIndex--;}
    private void Next_Click(object s,RoutedEventArgs e){if(RunList.SelectedIndex<_run.Count-1)RunList.SelectedIndex++;}
    private void SendLive_Click(object s,RoutedEventArgs e)=>SendLive(); private void Black_Click(object s,RoutedEventArgs e)=>RenderState(_live.State==PresentationState.Black?PresentationState.Empty:PresentationState.Black); private void Clear_Click(object s,RoutedEventArgs e)=>RenderState(PresentationState.Empty); private void RemoveText_Click(object s,RoutedEventArgs e){LiveContent.Text="";_output?.Render(new(PresentationState.Content,_live.Title,"",_live.MediaPath));} private void Logo_Click(object s,RoutedEventArgs e)=>RenderState(PresentationState.Logo);
    private void OpenDisplay_Click(object s,RoutedEventArgs e)=>OpenDisplay();
    private void OpenDisplay(){_output?.Close();_output=new OutputWindow();var screens=Forms.Screen.AllScreens;var target=screens.FirstOrDefault(x=>!x.Primary)??screens[0];_output.Left=target.Bounds.Left;_output.Top=target.Bounds.Top;_output.Width=target.Bounds.Width;_output.Height=target.Bounds.Height;_output.Show();_output.WindowState=WindowState.Maximized;_output.Render(_live);DisplayStateText.Text=screens.Length>1?"Pantalla externa conectada":"Salida en este monitor";StatusText.Text=$"Salida iniciada en {target.DeviceName}";}
    private void Export_Click(object s,RoutedEventArgs e){SaveNow();var d=new SaveFileDialog{Filter="Copia de Cultos|*.cultos",FileName=_service.Name.Replace(' ','_')+".cultos"};if(d.ShowDialog()==true){_db.ExportService(_service,d.FileName);StatusText.Text="Copia de seguridad exportada";}}
    private void Import_Click(object s,RoutedEventArgs e){var d=new OpenFileDialog{Filter="Copia de Cultos|*.cultos"};if(d.ShowDialog()==true)try{_service=_db.ImportService(d.FileName);ServiceNameText.Text=_service.Name;RefreshRun();StatusText.Text="Copia importada correctamente";}catch(Exception ex){MessageBox.Show("No se pudo importar la copia. "+ex.Message,"Importación fallida",MessageBoxButton.OK,MessageBoxImage.Error);}}
    private void Services_Click(object s,RoutedEventArgs e)=>MessageBox.Show("El culto activo se guarda automáticamente. La lista completa queda pendiente.","Cultos guardados");
    private void Settings_Click(object s,RoutedEventArgs e)=>MessageBox.Show("Funciona sin internet. Datos: AppData\\Local\\Cultos.","Configuración local");
    private void Window_KeyDown(object s,System.Windows.Input.KeyEventArgs e){if(Keyboard.FocusedElement is System.Windows.Controls.TextBox)return;if(e.Key==Key.Right)Next_Click(s,e);else if(e.Key==Key.Left)Previous_Click(s,e);else if(e.Key==Key.Space)SendLive();else if(e.Key==Key.B)Black_Click(s,e);else if(e.Key==Key.C)Clear_Click(s,e);else if(e.Key==Key.F5)OpenDisplay();else if(e.Key==Key.Escape&&_live.State==PresentationState.Black)RenderState(PresentationState.Empty);}
}
public sealed record LibraryRow(string Title,string Subtitle,object Source);
public sealed class RunRow
{
    public RunRow(ServiceItem item) => Item = item;
    public ServiceItem Item { get; }
    public string Title => Item.Title;
    public string Status => Item.Status;
    public string TypeLabel => Item.Type switch { ContentType.Bible=>"Texto bíblico", ContentType.Hymn=>"Himno", ContentType.Image=>"Imagen", ContentType.Video=>"Video", ContentType.Welcome=>"Bienvenida", _=>"Texto libre" };
}
