using Cultos.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Cultos.App;

public partial class MainWindow : Window
{
    private readonly LocalDatabase _db;
    private readonly string _dataFolder;
    private WorshipService _service;
    private readonly ObservableCollection<RunRow> _run = [];
    private string _mode = "Bible";
    private string? _currentMediaFolder;
    private ServiceItem? _preview;
    private PresentationSnapshot _live = new(PresentationState.Empty, "Sin contenido", "");
    private ContentType? _liveType;
    private OutputWindow? _output;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private GridLength _orderPanelWidth = new(350);

    public MainWindow()
    {
        InitializeComponent();
        _dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cultos");
        _db = new LocalDatabase(Path.Combine(_dataFolder, "cultos.db"));
        _db.Initialize();
        _service = _db.LoadActive() ?? CreateDemoService();
        if (_service.Id == 0) _db.SaveService(_service);

        RunList.ItemsSource = _run;
        RefreshRun();
        ConfigureMode("Bible");
        ServiceNameText.Text = _service.Name;

        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("h:mm tt");
        ClockText.Text = DateTime.Now.ToString("h:mm tt");
        _clock.Start();

        _saveTimer.Tick += (_, _) => SaveNow();
        Closed += (_, _) =>
        {
            SaveNow();
            _output?.Close();
        };
    }

    private static WorshipService CreateDemoService() => new()
    {
        Name = "Culto divino · Demostración",
        Items =
        [
            new(){Type=ContentType.Welcome,Title="Bienvenidos a casa",Content="Bienvenidos\nIglesia local",Status="Preparado"},
            new(){Type=ContentType.Hymn,Title="Himno inicial",Content="Selecciona un himno desde la biblioteca"},
            new(){Type=ContentType.FreeText,Title="Oración de apertura",Content="Oración de apertura"},
            new(){Type=ContentType.Bible,Title="Lectura bíblica",Content="Selecciona una lectura desde la biblioteca"},
            new(){Type=ContentType.FreeText,Title="Predicación",Content="Firmes en la esperanza"}
        ]
    };

    private void RefreshRun()
    {
        _run.Clear();
        foreach (var item in _service.Items) _run.Add(new(item));
        ItemCountText.Text = $"{_run.Count} elementos";
    }

    private void QueueSave()
    {
        SaveStateText.Text = "Guardando…";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer.Stop();
        _db.SaveService(_service);
        SaveStateText.Text = "Guardado";
        StatusText.Text = "Guardado local completado";
    }

    private void ConfigureMode(string mode)
    {
        _mode = mode;
        MediaToolbar.Visibility = mode == "Media" ? Visibility.Visible : Visibility.Collapsed;
        LibrarySecondaryButton.Visibility = Visibility.Collapsed;
        LibraryPrimaryButton.Visibility = Visibility.Visible;
        SearchBox.Visibility = Visibility.Visible;
        SectionLabel.Text = "BIBLIOTECA";

        switch (mode)
        {
            case "Home":
                SectionLabel.Text = "INICIO";
                LibraryTitle.Text = "Accesos rápidos";
                LibraryPrimaryButton.Content = "Abrir";
                SearchBox.Visibility = Visibility.Collapsed;
                break;
            case "Bible":
                LibraryTitle.Text = "Biblia";
                LibraryPrimaryButton.Content = "＋  Agregar al orden del culto";
                break;
            case "Hymn":
                LibraryTitle.Text = "Himnario";
                LibraryPrimaryButton.Content = "＋  Agregar al orden del culto";
                break;
            case "Song":
                LibraryTitle.Text = "Canciones";
                LibraryPrimaryButton.Content = "＋  Agregar al orden del culto";
                LibrarySecondaryButton.Content = "Nueva canción";
                LibrarySecondaryButton.Visibility = Visibility.Visible;
                break;
            case "Media":
                LibraryTitle.Text = "Multimedia";
                LibraryPrimaryButton.Content = "＋  Agregar al orden del culto";
                _currentMediaFolder = null;
                break;
            case "Design":
                LibraryTitle.Text = "Diseños rápidos";
                LibraryPrimaryButton.Content = "＋  Agregar al orden del culto";
                break;
            case "Services":
                SectionLabel.Text = "CULTOS";
                LibraryTitle.Text = "Cultos guardados";
                LibraryPrimaryButton.Content = "Abrir culto";
                LibrarySecondaryButton.Content = "Nuevo culto";
                LibrarySecondaryButton.Visibility = Visibility.Visible;
                break;
            case "Settings":
                SectionLabel.Text = "CONFIGURACIÓN";
                LibraryTitle.Text = "Sistema local";
                LibraryPrimaryButton.Content = "Abrir carpeta de datos";
                LibrarySecondaryButton.Content = "Importar copia";
                LibrarySecondaryButton.Visibility = Visibility.Visible;
                SearchBox.Visibility = Visibility.Collapsed;
                break;
        }

        SearchBox.Text = "";
        LoadLibrary();
    }

    private void LoadLibrary()
    {
        var query = SearchBox.Text ?? "";
        switch (_mode)
        {
            case "Home":
                LibraryList.ItemsSource = new[]
                {
                    new LibraryRow("Biblia","Buscar y proyectar textos bíblicos",new QuickAction("Bible")),
                    new LibraryRow("Himnos","Buscar y proyectar himnos",new QuickAction("Hymn")),
                    new LibraryRow("Canciones","Biblioteca local de canciones",new QuickAction("Song")),
                    new LibraryRow("Multimedia","Imágenes y videos del equipo",new QuickAction("Media")),
                    new LibraryRow("Cultos guardados","Crear o abrir órdenes de culto",new QuickAction("Services"))
                };
                break;
            case "Bible":
                LibraryList.ItemsSource = _db.SearchBible(query).Select(v => new LibraryRow(v.Reference, v.Text, v)).ToList();
                break;
            case "Hymn":
                LibraryList.ItemsSource = _db.SearchHymns(query).Select(h => new LibraryRow($"{h.Number} · {h.Title}", h.Lyrics, h)).ToList();
                break;
            case "Song":
                LibraryList.ItemsSource = _db.SearchSongs(query).Select(s => new LibraryRow(s.Title, string.IsNullOrWhiteSpace(s.Author) ? s.Lyrics : $"{s.Author} · {s.Lyrics}", s)).ToList();
                break;
            case "Media":
                LoadMediaBrowser(query);
                break;
            case "Design":
                LibraryList.ItemsSource = BuiltInDesigns()
                    .Where(x => string.IsNullOrWhiteSpace(query) || (x.Title + " " + x.Content).Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Select(x => new LibraryRow(x.Title, x.Content.Replace("\n", " · "), x)).ToList();
                break;
            case "Services":
                LibraryList.ItemsSource = _db.ListServices()
                    .Where(s => string.IsNullOrWhiteSpace(query) || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Select(s => new LibraryRow(s.Name, $"{s.Date:d} · actualizado {s.UpdatedAt:g}", s)).ToList();
                break;
            case "Settings":
                LibraryList.ItemsSource = new[]
                {
                    new LibraryRow("Modo sin conexión","La aplicación funciona con datos locales.",new SettingInfo("offline")),
                    new LibraryRow("Datos locales",_dataFolder,new SettingInfo("data")),
                    new LibraryRow("Pantallas detectadas",$"{Forms.Screen.AllScreens.Length} pantalla(s) disponible(s)",new SettingInfo("screens")),
                    new LibraryRow("Atajos","←/→ navegar · Espacio enviar · B negra · C limpiar · F5 pantalla",new SettingInfo("keys"))
                };
                break;
        }
    }

    private static IReadOnlyList<DesignPreset> BuiltInDesigns() =>
    [
        new("Bienvenida","Bienvenidos\nQué alegría tenerte con nosotros",ContentType.Welcome),
        new("Momento de oración","Momento de oración",ContentType.FreeText),
        new("Diezmos y ofrendas","Diezmos y ofrendas\nAdoramos a Dios también con nuestros recursos",ContentType.FreeText),
        new("Anuncios","Anuncios de la iglesia",ContentType.FreeText),
        new("Despedida","Gracias por acompañarnos\nQue Dios te bendiga",ContentType.FreeText)
    ];

    private ServiceItem? LibrarySelection()
    {
        if (LibraryList.SelectedItem is not LibraryRow row) return null;
        return row.Source switch
        {
            BibleVerse v => new() { Type = ContentType.Bible, Title = v.Reference, Content = v.Text, Status = "Preparado" },
            Hymn h => new() { Type = ContentType.Hymn, Title = $"{h.Number} · {h.Title}", Content = h.Lyrics, Status = "Preparado" },
            LibrarySong song => new() { Type = ContentType.Song, Title = song.Title, Content = song.Lyrics, Status = "Preparado" },
            DesignPreset d => new() { Type = d.Type, Title = d.Title, Content = d.Content, Status = "Preparado" },
            FileSystemEntry file when !file.IsFolder && File.Exists(file.Path) => new()
            {
                Type = file.Kind == "Imagen" ? ContentType.Image : ContentType.Video,
                Title = file.Name,
                Content = file.Kind,
                MediaPath = file.Path,
                Status = "Preparado"
            },
            _ => null
        };
    }

    private void MissingMedia()
    {
        StatusText.Text = "Archivo multimedia ausente · vuelve a localizarlo";
        MessageBox.Show("El archivo fue movido o eliminado. Vuelve a agregarlo desde Multimedia.", "Archivo no encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ResetPreviewMedia()
    {
        PreviewVideo.Stop();
        PreviewVideo.Source = null;
        PreviewVideo.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewContent.Visibility = Visibility.Visible;
    }

    private void ShowPreview(ServiceItem item)
    {
        if ((item.Type == ContentType.Image || item.Type == ContentType.Video) && (string.IsNullOrWhiteSpace(item.MediaPath) || !File.Exists(item.MediaPath)))
        {
            _preview = null;
            ResetPreviewMedia();
            PreviewTitle.Text = "Archivo no encontrado";
            PreviewContent.Text = "Vuelve a agregar este archivo desde Multimedia.";
            MissingMedia();
            return;
        }

        _preview = item;
        PreviewTitle.Text = item.Title;
        ResetPreviewMedia();

        if (item.Type == ContentType.Image)
        {
            PreviewContent.Visibility = Visibility.Collapsed;
            PreviewImage.Source = new BitmapImage(new Uri(item.MediaPath!));
            PreviewImage.Visibility = Visibility.Visible;
        }
        else if (item.Type == ContentType.Video)
        {
            PreviewContent.Visibility = Visibility.Collapsed;
            PreviewVideo.Source = new Uri(item.MediaPath!);
            PreviewVideo.Visibility = Visibility.Visible;
        }
        else
        {
            PreviewContent.Text = item.Content;
        }
    }

    private void SendLive()
    {
        if (_preview is null) return;
        if ((_preview.Type == ContentType.Image || _preview.Type == ContentType.Video) && (string.IsNullOrWhiteSpace(_preview.MediaPath) || !File.Exists(_preview.MediaPath)))
        {
            MissingMedia();
            return;
        }

        _live = new(PresentationState.Content, _preview.Title, _preview.Content, _preview.MediaPath);
        _liveType = _preview.Type;
        _preview.Status = "Presentado";
        LiveTitle.Text = "  " + _preview.Title;
        ShowLiveMedia(_preview);
        LiveBadge.Visibility = Visibility.Visible;
        _output?.Render(_live, _preview.Type);
        RefreshRun();
        QueueSave();
        StatusText.Text = "Contenido enviado a la congregación";
    }

    private void ShowLiveMedia(ServiceItem item)
    {
        LiveVideo.Stop();
        LiveVideo.Source = null;
        LiveVideo.Visibility = Visibility.Collapsed;
        LiveImage.Source = null;
        LiveImage.Visibility = Visibility.Collapsed;
        LiveContent.Visibility = Visibility.Visible;

        if (item.Type == ContentType.Image && File.Exists(item.MediaPath))
        {
            LiveContent.Visibility = Visibility.Collapsed;
            LiveImage.Source = new BitmapImage(new Uri(item.MediaPath!));
            LiveImage.Visibility = Visibility.Visible;
        }
        else if (item.Type == ContentType.Video && File.Exists(item.MediaPath))
        {
            LiveContent.Visibility = Visibility.Collapsed;
            LiveVideo.Source = new Uri(item.MediaPath!);
            LiveVideo.Visibility = Visibility.Visible;
            LiveVideo.Play();
        }
        else
        {
            LiveContent.Text = item.Content;
        }
    }

    private void RenderState(PresentationState state)
    {
        _live = state switch
        {
            PresentationState.Black => new(state, "Pantalla negra", ""),
            PresentationState.Logo => new(state, "Logotipo", "Iglesia local"),
            _ => new(PresentationState.Empty, "Sin contenido", "")
        };
        _liveType = null;
        LiveVideo.Stop();
        LiveVideo.Visibility = Visibility.Collapsed;
        LiveImage.Visibility = Visibility.Collapsed;
        LiveContent.Visibility = Visibility.Visible;
        LiveTitle.Text = "  " + _live.Title;
        LiveContent.Text = state == PresentationState.Black ? "Pantalla negra" : state == PresentationState.Logo ? "Logotipo de la iglesia" : "Sin contenido";
        LiveBadge.Visibility = state == PresentationState.Empty ? Visibility.Collapsed : Visibility.Visible;
        _output?.Render(_live);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsLoaded) LoadLibrary();
    }

    private void OrderNav_Click(object sender, RoutedEventArgs e)
    {
        var show = OrderPanel.Visibility != Visibility.Visible;
        if (show)
        {
            OrderColumn.MinWidth = 240;
            OrderColumn.Width = _orderPanelWidth.Value > 0 ? _orderPanelWidth : new GridLength(350);
            OrderSplitterColumn.Width = new GridLength(5);
            OrderPanel.Visibility = Visibility.Visible;
            OrderSplitter.Visibility = Visibility.Visible;
            StatusText.Text = "Orden del culto visible";
        }
        else
        {
            if (OrderColumn.ActualWidth > 0)
                _orderPanelWidth = OrderColumn.Width.Value > 0 ? OrderColumn.Width : new GridLength(OrderColumn.ActualWidth);
            OrderPanel.Visibility = Visibility.Collapsed;
            OrderSplitter.Visibility = Visibility.Collapsed;
            OrderColumn.MinWidth = 0;
            OrderColumn.Width = new GridLength(0);
            OrderSplitterColumn.Width = new GridLength(0);
            StatusText.Text = "Orden del culto oculto";
        }
    }

    private void HomeNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Home");
    private void BibleNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Bible");
    private void HymnNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Hymn");
    private void SongsNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Song");
    private void MediaNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Media");
    private void DesignsNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Design");
    private void Services_Click(object sender, RoutedEventArgs e) => ConfigureMode("Services");
    private void Settings_Click(object sender, RoutedEventArgs e) => ConfigureMode("Settings");

    private void LoadMediaBrowser(string query = "")
    {
        var entries = new List<FileSystemEntry>();
        if (_currentMediaFolder is null)
        {
            var shortcuts = new[]
            {
                ("Escritorio",Environment.SpecialFolder.DesktopDirectory),
                ("Documentos",Environment.SpecialFolder.MyDocuments),
                ("Imágenes",Environment.SpecialFolder.MyPictures),
                ("Videos",Environment.SpecialFolder.MyVideos)
            };
            foreach (var (name, special) in shortcuts)
            {
                var path = Environment.GetFolderPath(special);
                if (Directory.Exists(path)) entries.Add(new(name, path, true, "Carpeta del sistema"));
            }

            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                entries.Add(new(drive.Name, drive.RootDirectory.FullName, true, $"Unidad · {FormatBytes(drive.AvailableFreeSpace)} libres"));

            MediaPathText.Text = "Este equipo";
        }
        else
        {
            MediaPathText.Text = _currentMediaFolder;
            try
            {
                entries.AddRange(Directory.EnumerateDirectories(_currentMediaFolder).Select(p => new FileSystemEntry(Path.GetFileName(p), p, true, "Carpeta")));
                var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".mp4", ".wmv", ".avi", ".mov", ".mkv" };
                entries.AddRange(Directory.EnumerateFiles(_currentMediaFolder)
                    .Where(p => supported.Contains(Path.GetExtension(p)))
                    .Select(p =>
                    {
                        var video = new[] { ".mp4", ".wmv", ".avi", ".mov", ".mkv" }.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase);
                        return new FileSystemEntry(Path.GetFileName(p), p, false, video ? "Video" : "Imagen", new FileInfo(p).Length);
                    }));
            }
            catch (UnauthorizedAccessException)
            {
                StatusText.Text = "Windows no permite acceder a esta carpeta";
            }
            catch (IOException ex)
            {
                StatusText.Text = "No se pudo abrir la carpeta · " + ex.Message;
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
            entries = entries.Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        LibraryList.ItemsSource = entries.OrderByDescending(e => e.IsFolder).ThenBy(e => e.Name)
            .Select(e => new LibraryRow(e.IsFolder ? "▱  " + e.Name : e.Name, e.IsFolder ? e.Kind : $"{e.Kind} · {FormatBytes(e.Size)}", e)).ToList();
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1073741824 => $"{bytes / 1073741824d:0.0} GB",
        >= 1048576 => $"{bytes / 1048576d:0.0} MB",
        >= 1024 => $"{bytes / 1024d:0} KB",
        _ => $"{bytes} B"
    };

    private void MediaUp_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMediaFolder is null) return;
        var parent = Directory.GetParent(_currentMediaFolder);
        _currentMediaFolder = parent?.FullName;
        SearchBox.Text = "";
        LoadMediaBrowser();
    }

    private void RefreshMedia_Click(object sender, RoutedEventArgs e) => LoadMediaBrowser(SearchBox.Text ?? "");

    private void LibraryList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_mode is "Home" or "Services" or "Settings") return;
        var item = LibrarySelection();
        if (item is not null) ShowPreview(item);
    }

    private void LibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LibraryList.SelectedItem is not LibraryRow row) return;

        if (row.Source is FileSystemEntry folder && folder.IsFolder)
        {
            _currentMediaFolder = folder.Path;
            SearchBox.Text = "";
            LoadMediaBrowser();
            return;
        }

        if (_mode is "Home" or "Services" or "Settings")
        {
            ExecutePrimaryLibraryAction();
            return;
        }

        var item = LibrarySelection();
        if (item is not null)
        {
            ShowPreview(item);
            SendLive();
        }
    }

    private void AddLibraryItem_Click(object sender, RoutedEventArgs e) => ExecutePrimaryLibraryAction();

    private void ExecutePrimaryLibraryAction()
    {
        if (_mode == "Home")
        {
            if (LibraryList.SelectedItem is LibraryRow { Source: QuickAction action }) ConfigureMode(action.Mode);
            return;
        }

        if (_mode == "Services")
        {
            OpenSelectedService();
            return;
        }

        if (_mode == "Settings")
        {
            OpenDataFolder();
            return;
        }

        var item = LibrarySelection();
        if (item is null) return;
        _service.Items.Add(item);
        RefreshRun();
        RunList.SelectedIndex = _run.Count - 1;
        QueueSave();
        StatusText.Text = "Elemento agregado al orden";
    }

    private void LibrarySecondary_Click(object sender, RoutedEventArgs e)
    {
        if (_mode == "Services") CreateNewService();
        else if (_mode == "Song") CreateSong();
        else if (_mode == "Settings") Import_Click(sender, e);
    }

    private void CreateSong()
    {
        var editor = new TextEditorWindow("Nueva canción", "", "", true, "Título", "Letra") { Owner = this };
        if (editor.ShowDialog() != true) return;
        _db.SaveSong(editor.ValueTitle, "", editor.ValueContent);
        LoadLibrary();
        StatusText.Text = "Canción guardada en la biblioteca local";
    }

    private void CreateNewService()
    {
        var editor = new TextEditorWindow("Nuevo culto", $"Culto {DateTime.Now:dd/MM/yyyy}", "", false, "Nombre del culto") { Owner = this };
        if (editor.ShowDialog() != true) return;

        SaveNow();
        _service = new WorshipService { Name = editor.ValueTitle, Date = DateTime.Today };
        _db.SaveService(_service);
        ServiceNameText.Text = _service.Name;
        RefreshRun();
        ClearPreview();
        LoadLibrary();
        StatusText.Text = "Nuevo culto creado";
    }

    private void OpenSelectedService()
    {
        if (LibraryList.SelectedItem is not LibraryRow { Source: WorshipService summary }) return;
        SaveNow();
        var loaded = _db.LoadService(summary.Id);
        if (loaded is null) return;
        _service = loaded;
        ServiceNameText.Text = _service.Name;
        RefreshRun();
        ClearPreview();
        StatusText.Text = "Culto abierto";
    }

    private void OpenDataFolder()
    {
        Directory.CreateDirectory(_dataFolder);
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", _dataFolder) { UseShellExecute = true });
            StatusText.Text = "Carpeta de datos abierta";
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo abrir la carpeta. " + ex.Message, "Cultos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearPreview()
    {
        _preview = null;
        ResetPreviewMedia();
        PreviewTitle.Text = "Selecciona contenido";
        PreviewContent.Text = "Selecciona un elemento para preparar la salida";
        RunList.SelectedIndex = -1;
    }

    private void RunList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (RunList.SelectedItem is RunRow row) ShowPreview(row.Item);
    }

    private void AddFreeText_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TextEditorWindow("Agregar texto libre", "Texto libre", "", true, "Título", "Texto a proyectar") { Owner = this };
        if (editor.ShowDialog() != true) return;

        var item = new ServiceItem
        {
            Type = ContentType.FreeText,
            Title = editor.ValueTitle,
            Content = editor.ValueContent,
            Status = "Preparado"
        };
        _service.Items.Add(item);
        RefreshRun();
        RunList.SelectedIndex = _run.Count - 1;
        QueueSave();
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        var index = RunList.SelectedIndex;
        if (index < 0) return;
        var item = _service.Items[index];
        var mediaOnly = item.Type is ContentType.Image or ContentType.Video;
        var editor = new TextEditorWindow("Editar elemento", item.Title, item.Content, !mediaOnly, "Título", "Contenido") { Owner = this };
        if (editor.ShowDialog() != true) return;

        item.Title = editor.ValueTitle;
        if (!mediaOnly) item.Content = editor.ValueContent;
        item.Status = "Preparado";
        RefreshRun();
        RunList.SelectedIndex = index;
        ShowPreview(item);
        QueueSave();
        StatusText.Text = "Elemento actualizado";
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        var index = RunList.SelectedIndex;
        if (index < 0) return;
        var next = index + delta;
        if (next < 0 || next >= _service.Items.Count) return;
        (_service.Items[index], _service.Items[next]) = (_service.Items[next], _service.Items[index]);
        RefreshRun();
        RunList.SelectedIndex = next;
        QueueSave();
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        var index = RunList.SelectedIndex;
        if (index < 0) return;
        if (MessageBox.Show("¿Quitar este elemento del orden?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _service.Items.RemoveAt(index);
        RefreshRun();
        ClearPreview();
        QueueSave();
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (RunList.SelectedIndex > 0) RunList.SelectedIndex--;
        else if (RunList.SelectedIndex < 0 && _run.Count > 0) RunList.SelectedIndex = 0;
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (RunList.SelectedIndex < 0 && _run.Count > 0) RunList.SelectedIndex = 0;
        else if (RunList.SelectedIndex < _run.Count - 1) RunList.SelectedIndex++;
    }

    private void SendLive_Click(object sender, RoutedEventArgs e) => SendLive();

    private void Black_Click(object sender, RoutedEventArgs e) =>
        RenderState(_live.State == PresentationState.Black ? PresentationState.Empty : PresentationState.Black);

    private void Clear_Click(object sender, RoutedEventArgs e) => RenderState(PresentationState.Empty);

    private void RemoveText_Click(object sender, RoutedEventArgs e)
    {
        if (_liveType is ContentType.Image or ContentType.Video) return;
        _live = new(PresentationState.Content, _live.Title, "", _live.MediaPath);
        LiveContent.Text = "";
        _output?.Render(_live, _liveType);
        StatusText.Text = "Texto retirado de la salida";
    }

    private void Logo_Click(object sender, RoutedEventArgs e) => RenderState(PresentationState.Logo);

    private void PlayMedia_Click(object sender, RoutedEventArgs e)
    {
        if (PreviewVideo.Visibility == Visibility.Visible) PreviewVideo.Play();
        if (LiveVideo.Visibility == Visibility.Visible) LiveVideo.Play();
        _output?.PlayMedia();
        StatusText.Text = "Reproduciendo video";
    }

    private void PauseMedia_Click(object sender, RoutedEventArgs e)
    {
        PreviewVideo.Pause();
        LiveVideo.Pause();
        _output?.PauseMedia();
        StatusText.Text = "Video en pausa";
    }

    private void StopMedia_Click(object sender, RoutedEventArgs e)
    {
        PreviewVideo.Stop();
        LiveVideo.Stop();
        _output?.StopMedia();
        StatusText.Text = "Video detenido";
    }

    private void OpenDisplay_Click(object sender, RoutedEventArgs e) => OpenDisplay();

    private void OpenDisplay()
    {
        _output?.Close();
        _output = new OutputWindow();
        _output.Closed += (_, _) =>
        {
            _output = null;
            DisplayStateText.Text = "Pantalla cerrada";
            StatusText.Text = "Salida externa cerrada";
        };

        var screens = Forms.Screen.AllScreens;
        var target = screens.FirstOrDefault(x => !x.Primary) ?? screens[0];
        _output.Left = target.Bounds.Left;
        _output.Top = target.Bounds.Top;
        _output.Width = target.Bounds.Width;
        _output.Height = target.Bounds.Height;
        _output.Show();
        _output.WindowState = WindowState.Maximized;
        _output.Render(_live, _liveType);
        DisplayStateText.Text = screens.Length > 1 ? "Pantalla externa conectada" : "Salida en este monitor";
        StatusText.Text = $"Salida iniciada en {target.DeviceName}";
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        SaveNow();
        var dialog = new SaveFileDialog
        {
            Filter = "Copia de Cultos|*.cultos",
            FileName = _service.Name.Replace(' ', '_') + ".cultos"
        };
        if (dialog.ShowDialog() == true)
        {
            _db.ExportService(_service, dialog.FileName);
            StatusText.Text = "Copia de seguridad exportada";
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Copia de Cultos|*.cultos" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _service = _db.ImportService(dialog.FileName);
            ServiceNameText.Text = _service.Name;
            RefreshRun();
            ClearPreview();
            ConfigureMode("Services");
            StatusText.Text = "Copia importada correctamente";
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo importar la copia. " + ex.Message, "Importación fallida", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;
        if (e.Key == Key.Right) Next_Click(sender, e);
        else if (e.Key == Key.Left) Previous_Click(sender, e);
        else if (e.Key == Key.Space) SendLive();
        else if (e.Key == Key.B) Black_Click(sender, e);
        else if (e.Key == Key.C) Clear_Click(sender, e);
        else if (e.Key == Key.F5) OpenDisplay();
        else if (e.Key == Key.Escape && _live.State == PresentationState.Black) RenderState(PresentationState.Empty);
    }
}

public sealed record LibraryRow(string Title, string Subtitle, object Source);
public sealed record FileSystemEntry(string Name, string Path, bool IsFolder, string Kind, long Size = 0);
public sealed record QuickAction(string Mode);
public sealed record SettingInfo(string Key);
public sealed record DesignPreset(string Title, string Content, ContentType Type);

public sealed class RunRow
{
    public RunRow(ServiceItem item) => Item = item;
    public ServiceItem Item { get; }
    public string Title => Item.Title;
    public string Status => Item.Status;
    public string TypeLabel => Item.Type switch
    {
        ContentType.Bible => "Texto bíblico",
        ContentType.Hymn => "Himno",
        ContentType.Song => "Canción",
        ContentType.Image => "Imagen",
        ContentType.Video => "Video",
        ContentType.Welcome => "Bienvenida",
        ContentType.Background => "Fondo",
        _ => "Texto libre"
    };
}
