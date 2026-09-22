using Cultos.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly ChurchProfileStore _profileStore;
    private ChurchProfile _churchProfile;
    private readonly DisplayManager _displayManager;
    private readonly RemoteControlServer _remoteServer = new();
    private WorshipService _service;
    private readonly ObservableCollection<RunRow> _run = [];
    private readonly ObservableCollection<SceneRow> _sceneRows = [];
    private List<PresentationScene> _scenes = [];
    private string? _activeSceneKey;
    private string? _activeSceneBeforeBlack;
    private string _mode = "Bible";
    private string? _currentMediaFolder;
    private ServiceItem? _preview;
    private IReadOnlyList<string> _previewSlides = Array.Empty<string>();
    private int _previewSlideIndex;
    private PresentationSnapshot _live = new(PresentationState.Empty, "Sin contenido", "");
    private ContentType? _liveType;
    private PresentationSnapshot? _blackRestoreSnapshot;
    private ContentType? _blackRestoreType;
    private OutputWindow? _output;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _mediaTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _logoTimer = new();
    private List<string> _logoPlaylist = [];
    private int _logoIndex;
    private bool _logoLoop = true;
    private System.Windows.Point _libraryDragStart;
    private readonly string _sessionMarkerPath;
    private readonly bool _recoveredAfterUnexpectedExit;
    private static readonly HashSet<string> SupportedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif",
        ".mp4", ".wmv", ".avi", ".mov", ".mkv",
        ".mp3", ".wav", ".m4a", ".aac", ".wma", ".flac"
    };

    public MainWindow()
    {
        _dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cultos");
        Directory.CreateDirectory(_dataFolder);
        _profileStore = new ChurchProfileStore(_dataFolder);
        _churchProfile = _profileStore.Load();
        ApplyAccentResource(_churchProfile.AccentHex);

        InitializeComponent();
        _sessionMarkerPath = Path.Combine(_dataFolder, "session.running");
        _recoveredAfterUnexpectedExit = File.Exists(_sessionMarkerPath);
        try { File.WriteAllText(_sessionMarkerPath, DateTime.Now.ToString("O")); }
        catch (Exception ex) { AppLogger.Error("No se pudo crear el marcador de sesión", ex); }

        _settingsStore = new AppSettingsStore(_dataFolder);
        _settings = _settingsStore.Load();
        if (string.IsNullOrWhiteSpace(_settings.RemoteControlPin))
        {
            _settings.RemoteControlPin = RemoteControlServer.GeneratePin();
            _settingsStore.Save(_settings);
        }

        _displayManager = new DisplayManager(_settings);
        _remoteServer.CommandReceived += RemoteServer_CommandReceived;
        ApplyWindowSettings();

        _db = new LocalDatabase(Path.Combine(_dataFolder, "cultos.db"));
        _db.Initialize();
        _service = _db.LoadActive() ?? CreateDemoService();
        if (_service.Id == 0) _db.SaveService(_service);

        RunList.ItemsSource = _run;
        SceneList.ItemsSource = _sceneRows;
        RefreshRun();
        LoadScenes();
        ConfigureMode(IsKnownMode(_settings.LastMode) ? _settings.LastMode : "Bible");
        ServiceNameText.Text = _service.Name;
        ApplyChurchProfile();
        MediaVolumeSlider.Value = Math.Clamp(_settings.MediaVolume, 0, 1);

        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("h:mm tt");
        ClockText.Text = DateTime.Now.ToString("h:mm tt");
        _clock.Start();

        _saveTimer.Tick += (_, _) => SaveNow();
        _mediaTimer.Tick += MediaTimer_Tick;
        _mediaTimer.Start();
        _logoTimer.Tick += LogoTimer_Tick;

        Loaded += async (_, _) =>
        {
            ApplyMediaVolume();
            SetMonitorPanelVisibility(_settings.MonitorPanelVisible);
            if (_settings.MainMaximized) WindowState = WindowState.Maximized;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            if (_settings.RemoteControlEnabled)
                await StartRemoteControlFromSettingsAsync();

            PublishRemoteState();

            if (_recoveredAfterUnexpectedExit)
                StatusText.Text = "Sesión anterior recuperada después de un cierre inesperado";
        };

        Closed += (_, _) =>
        {
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            _mediaTimer.Stop();
            _logoTimer.Stop();
            SaveNow();
            SaveWindowSettings();
            _output?.Close();
            try
            {
                _remoteServer.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                AppLogger.Error("No se pudo detener el control remoto al cerrar", ex);
            }

            try
            {
                if (File.Exists(_sessionMarkerPath)) File.Delete(_sessionMarkerPath);
            }
            catch (Exception ex)
            {
                AppLogger.Error("No se pudo limpiar el marcador de sesión", ex);
            }
        };
    }

    private static bool IsKnownMode(string mode) =>
        mode is "Bible" or "Hymn" or "Song" or "Media" or "YouTube" or "Design" or "Services" or "Settings";

    private void ApplyWindowSettings()
    {
        Width = Math.Max(MinWidth, _settings.MainWidth);
        Height = Math.Max(MinHeight, _settings.MainHeight);

        if (_settings.MainLeft is not double left || _settings.MainTop is not double top) return;

        var maxLeft = SystemParameters.VirtualScreenLeft + Math.Max(0, SystemParameters.VirtualScreenWidth - Math.Min(Width, SystemParameters.VirtualScreenWidth));
        var maxTop = SystemParameters.VirtualScreenTop + Math.Max(0, SystemParameters.VirtualScreenHeight - Math.Min(Height, SystemParameters.VirtualScreenHeight));

        Left = Math.Clamp(left, SystemParameters.VirtualScreenLeft, maxLeft);
        Top = Math.Clamp(top, SystemParameters.VirtualScreenTop, maxTop);
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    private void SaveWindowSettings()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!bounds.IsEmpty)
        {
            _settings.MainLeft = bounds.Left;
            _settings.MainTop = bounds.Top;
            _settings.MainWidth = Math.Max(MinWidth, bounds.Width);
            _settings.MainHeight = Math.Max(MinHeight, bounds.Height);
        }

        _settings.MainMaximized = WindowState == WindowState.Maximized;
        _settings.LastMode = _mode;
        _settings.MediaVolume = MediaVolumeSlider.Value;
        _settings.MonitorPanelVisible = MonitorPanel.Visibility == Visibility.Visible;
        _settingsStore.Save(_settings);
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
        PublishRemoteState();
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
        try
        {
            _db.SaveService(_service);
            SaveStateText.Text = "Guardado";
            StatusText.Text = "Guardado local completado";
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo guardar el culto activo", ex);
            SaveStateText.Text = "Error al guardar";
            StatusText.Text = "No se pudo guardar · revisa los registros";
        }
    }

    private static void ApplyAccentResource(string accentHex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(accentHex);
            Application.Current.Resources["Accent"] = new System.Windows.Media.SolidColorBrush(color);

            var dark = System.Windows.Media.Color.FromRgb(
                (byte)Math.Clamp(color.R * 0.24, 0, 255),
                (byte)Math.Clamp(color.G * 0.24, 0, 255),
                (byte)Math.Clamp(color.B * 0.24, 0, 255));
            Application.Current.Resources["AccentDark"] = new System.Windows.Media.SolidColorBrush(dark);
        }
        catch
        {
            // Si el perfil contiene un color inválido, se conservan los recursos predeterminados.
        }
    }

    private void ApplyChurchProfile()
    {
        ChurchNameText.Text = string.IsNullOrWhiteSpace(_churchProfile.Name)
            ? "Iglesia local"
            : _churchProfile.Name;
        ApplyAccentResource(_churchProfile.AccentHex);

        var background = LoadProfileBitmap(_churchProfile.DefaultBackgroundPath);
        PreviewBackgroundImage.Source = background;
        LiveBackgroundImage.Source = background;
        _output?.SetDefaultBackground(_churchProfile.DefaultBackgroundPath);
    }

    private static BitmapImage? LoadProfileBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo cargar un recurso visual del perfil", ex);
            return null;
        }
    }

    private void ChurchProfile_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ChurchProfileWindow(_profileStore, _db) { Owner = this };
        editor.ShowDialog();

        if (!editor.ProfileChanged) return;

        StopLogoSlideshow();
        _churchProfile = _profileStore.Load();
        ApplyChurchProfile();
        LoadScenes();
        if (_mode == "Settings") LoadLibrary();
        PublishRemoteState();
        StatusText.Text = "Perfil de iglesia actualizado";
    }

    private async Task<RemoteControlStatus> StartRemoteControlFromSettingsAsync()
    {
        var status = await _remoteServer.StartAsync(
            _settings.RemoteControlPort,
            _settings.RemoteControlPin);

        if (!status.IsRunning)
        {
            _settings.RemoteControlEnabled = false;
            _settingsStore.Save(_settings);
            StatusText.Text = "No se pudo iniciar el control remoto";
            return status;
        }

        PublishRemoteState();
        StatusText.Text = $"Control remoto activo · {status.Url}";
        return status;
    }

    private async Task<RemoteControlStatus> ApplyRemoteControlSettingsAsync(bool enabled, int port, string pin)
    {
        _settings.RemoteControlEnabled = enabled;
        _settings.RemoteControlPort = Math.Clamp(port, 1024, 65535);
        _settings.RemoteControlPin = pin;
        _settingsStore.Save(_settings);

        if (!enabled)
        {
            await _remoteServer.StopAsync();
            if (_mode == "Settings") LoadLibrary();
            StatusText.Text = "Control remoto detenido";
            return new RemoteControlStatus(false, "");
        }

        var status = await StartRemoteControlFromSettingsAsync();
        if (_mode == "Settings") LoadLibrary();
        return status;
    }

    private RemoteControlStatus CurrentRemoteControlStatus() =>
        _remoteServer.IsRunning
            ? new RemoteControlStatus(true, _remoteServer.Url)
            : new RemoteControlStatus(false, "");

    private void RemoteControl_Click(object sender, RoutedEventArgs e)
    {
        var window = new RemoteControlWindow(
            _settings,
            ApplyRemoteControlSettingsAsync,
            CurrentRemoteControlStatus)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void RemoteServer_CommandReceived(object? sender, RemoteCommand command)
    {
        Dispatcher.BeginInvoke(() => HandleRemoteCommand(command));
    }

    private void HandleRemoteCommand(RemoteCommand command)
    {
        switch (command.Command.ToLowerInvariant())
        {
            case "scene":
                var scene = _scenes.FirstOrDefault(x =>
                    string.Equals(x.Key, command.Value, StringComparison.OrdinalIgnoreCase));
                if (scene is not null) ActivateScene(scene);
                break;
            case "send":
                SendLive();
                break;
            case "previous":
                Previous_Click(this, new RoutedEventArgs());
                break;
            case "next":
                Next_Click(this, new RoutedEventArgs());
                break;
            case "play":
                PlayMedia_Click(this, new RoutedEventArgs());
                break;
            case "pause":
                PauseMedia_Click(this, new RoutedEventArgs());
                break;
            case "logo":
                Logo_Click(this, new RoutedEventArgs());
                break;
            case "black":
                Black_Click(this, new RoutedEventArgs());
                break;
            case "clear":
                Clear_Click(this, new RoutedEventArgs());
                break;
        }

        PublishRemoteState();
    }

    private void PublishRemoteState()
    {
        if (!_remoteServer.IsRunning) return;

        var scenes = _sceneRows
            .Select(row => new RemoteSceneState(
                row.Scene.Key,
                row.Name,
                row.Icon,
                row.StateLabel,
                row.IsLive))
            .ToList();

        _remoteServer.UpdateState(new RemoteControlState(
            string.IsNullOrWhiteSpace(_churchProfile.Name) ? "Cultos" : _churchProfile.Name,
            _service?.Name ?? "",
            _live.Title,
            _activeSceneKey,
            scenes));
    }

    private void ConfigureMode(string mode)
    {
        _mode = mode;
        LibraryPanel.Visibility = mode == "YouTube" ? Visibility.Collapsed : Visibility.Visible;
        OrderPanel.Visibility = Visibility.Collapsed;
        YouTubePanel.Visibility = mode == "YouTube" ? Visibility.Visible : Visibility.Collapsed;
        MediaToolbar.Visibility = mode == "Media" ? Visibility.Visible : Visibility.Collapsed;
        LibrarySecondaryButton.Visibility = Visibility.Collapsed;
        LibraryPrimaryButton.Visibility = Visibility.Visible;
        LibraryList.SelectionMode = System.Windows.Controls.SelectionMode.Single;
        SearchBox.Visibility = Visibility.Visible;
        SectionLabel.Text = "BIBLIOTECA";

        switch (mode)
        {
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
                LibraryTitle.Text = "Archivos del equipo";
                LibraryPrimaryButton.Content = "＋  Agregar al orden del culto";
                LibrarySecondaryButton.Content = "Agregar a Logo";
                LibrarySecondaryButton.Visibility = Visibility.Visible;
                LibraryList.SelectionMode = System.Windows.Controls.SelectionMode.Extended;
                _currentMediaFolder = null;
                break;
            case "YouTube":
                LoadPreparedYouTubeScene();
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
                LibraryTitle.Text = "Sistema y pantallas";
                LibraryPrimaryButton.Content = "Aplicar / Abrir";
                LibrarySecondaryButton.Content = "Importar copia";
                LibrarySecondaryButton.Visibility = Visibility.Visible;
                SearchBox.Visibility = Visibility.Collapsed;
                break;
        }

        UpdateNavSelection(mode);
        SearchBox.Text = "";
        LoadLibrary();
    }

    private void UpdateNavSelection(string mode)
    {
        var selectedBackground = (System.Windows.Media.Brush)FindResource("AccentDark");
        var selectedBorder = (System.Windows.Media.Brush)FindResource("Accent");
        var transparent = System.Windows.Media.Brushes.Transparent;

        var buttons = new (System.Windows.Controls.Button Button, string Mode)[]
        {
            (OrderNavButton, "Order"),
            (BibleNavButton, "Bible"),
            (HymnNavButton, "Hymn"),
            (SongsNavButton, "Song"),
            (MediaNavButton, "Media"),
            (YouTubeNavButton, "YouTube"),
            (DesignsNavButton, "Design"),
            (ServicesNavButton, "Services")
        };

        foreach (var (button, buttonMode) in buttons)
        {
            var selected = buttonMode == mode;
            button.Background = selected ? selectedBackground : transparent;
            button.BorderBrush = selected ? selectedBorder : transparent;
        }
    }

    private void LoadLibrary()
    {
        var query = SearchBox.Text ?? "";
        switch (_mode)
        {
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
                var selectedScreen = _displayManager.ResolvePresentationScreen().DeviceName;
                var settingsRows = new List<LibraryRow>
                {
                    new("Perfil de iglesia",_churchProfile.Name,new SettingInfo("profile")),
                    new("Control remoto",_remoteServer.IsRunning ? $"Activo · {_remoteServer.Url}" : "Detenido",new SettingInfo("remote")),
                    new("Modo sin conexión","La aplicación funciona completamente con datos locales.",new SettingInfo("offline")),
                    new("Datos locales",_dataFolder,new SettingInfo("data")),
                    new("Registros de errores",Path.Combine(_dataFolder, "logs"),new SettingInfo("logs")),
                    new("Identificar pantallas","Muestra durante unos segundos el número de cada monitor.",new SettingInfo("identify")),
                    new("Atajos","←/→ navegar · Espacio enviar · B negra · C limpiar · F5 pantalla",new SettingInfo("keys"))
                };
                settingsRows.AddRange(_displayManager.Screens.Select((screen, index) =>
                    new LibraryRow(
                        $"{(screen.DeviceName == selectedScreen ? "●" : "○")} Pantalla {index + 1}",
                        $"{DisplayManager.Describe(screen)} · {screen.DeviceName}",
                        new DisplayChoice(screen.DeviceName))));
                LibraryList.ItemsSource = settingsRows;
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
        return CreateServiceItem(row.Source);
    }

    private static ServiceItem? CreateServiceItem(object source) => source switch
    {
        BibleVerse v => new() { Type = ContentType.Bible, Title = v.Reference, Content = v.Text, Status = "Preparado" },
        Hymn h => new() { Type = ContentType.Hymn, Title = $"{h.Number} · {h.Title}", Content = h.Lyrics, Status = "Preparado" },
        LibrarySong song => new() { Type = ContentType.Song, Title = song.Title, Content = song.Lyrics, Status = "Preparado" },
        DesignPreset d => new() { Type = d.Type, Title = d.Title, Content = d.Content, Status = "Preparado" },
        FileSystemEntry file when !file.IsFolder && File.Exists(file.Path) => new()
        {
            Type = file.Kind switch
            {
                "Imagen" => ContentType.Image,
                "Audio" => ContentType.Audio,
                _ => ContentType.Video
            },
            Title = file.Name,
            Content = file.Kind,
            MediaPath = file.Path,
            Status = "Preparado"
        },
        _ => null
    };

    private static ServiceItem? CreateServiceItemFromPath(string path)
    {
        if (!File.Exists(path)) return null;
        var extension = Path.GetExtension(path);
        var type = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif" }
            .Contains(extension, StringComparer.OrdinalIgnoreCase)
            ? ContentType.Image
            : new[] { ".mp3", ".wav", ".m4a", ".aac", ".wma", ".flac" }
                .Contains(extension, StringComparer.OrdinalIgnoreCase)
                ? ContentType.Audio
                : new[] { ".mp4", ".wmv", ".avi", ".mov", ".mkv" }
                    .Contains(extension, StringComparer.OrdinalIgnoreCase)
                    ? ContentType.Video
                    : (ContentType?)null;

        return type is null
            ? null
            : new ServiceItem
            {
                Type = type.Value,
                Title = Path.GetFileName(path),
                Content = type.Value.ToString(),
                MediaPath = path,
                Status = "Preparado"
            };
    }

    private void MissingMedia()
    {
        StatusText.Text = "Archivo multimedia ausente · vuelve a localizarlo";
        MessageBox.Show("El archivo fue movido o eliminado. Vuelve a agregarlo desde Archivos.", "Archivo no encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        if (item.Type is ContentType.Image or ContentType.Video or ContentType.Audio &&
            (string.IsNullOrWhiteSpace(item.MediaPath) || !File.Exists(item.MediaPath)))
        {
            _preview = null;
            _previewSlides = Array.Empty<string>();
            _previewSlideIndex = 0;
            ResetPreviewMedia();
            PreviewTitle.Text = "Archivo no encontrado";
            PreviewContent.Text = "Vuelve a agregar este archivo desde Archivos.";
            MissingMedia();
            return;
        }

        _preview = item;
        _previewSlideIndex = 0;
        ResetPreviewMedia();

        if (item.Type == ContentType.Image)
        {
            _previewSlides = Array.Empty<string>();
            PreviewTitle.Text = item.Title;
            PreviewContent.Visibility = Visibility.Collapsed;
            PreviewImage.Source = new BitmapImage(new Uri(item.MediaPath!));
            PreviewImage.Visibility = Visibility.Visible;
        }
        else if (item.Type == ContentType.Video)
        {
            _previewSlides = Array.Empty<string>();
            PreviewTitle.Text = item.Title;
            PreviewContent.Visibility = Visibility.Collapsed;
            PreviewVideo.Source = new Uri(item.MediaPath!);
            PreviewVideo.Visibility = Visibility.Visible;
        }
        else if (item.Type == ContentType.Audio)
        {
            _previewSlides = Array.Empty<string>();
            PreviewTitle.Text = item.Title;
            PreviewVideo.Source = new Uri(item.MediaPath!);
            PreviewVideo.Visibility = Visibility.Visible;
            PreviewContent.Visibility = Visibility.Visible;
            PreviewContent.Text = "♫\n" + item.Title;
        }
        else if (item.Type == ContentType.Web)
        {
            _previewSlides = Array.Empty<string>();
            PreviewTitle.Text = item.Title;
            PreviewContent.Visibility = Visibility.Visible;
            PreviewContent.Text = "▷ YouTube\n" + item.Title;
        }
        else
        {
            _previewSlides = TextPaginator.Split(item.Content, 220);
            if (_previewSlides.Count == 0) _previewSlides = new[] { item.Content };
            RenderPreviewSlide();
        }

        PrepareSceneFromItem(item);
    }

    private void RenderPreviewSlide()
    {
        if (_preview is null || _previewSlides.Count == 0) return;
        _previewSlideIndex = Math.Clamp(_previewSlideIndex, 0, _previewSlides.Count - 1);
        PreviewTitle.Text = _previewSlides.Count > 1
            ? $"{_preview.Title} · {_previewSlideIndex + 1}/{_previewSlides.Count}"
            : _preview.Title;
        PreviewContent.Text = _previewSlides[_previewSlideIndex];
    }

    private string CurrentPreviewContent() =>
        _previewSlides.Count > 0
            ? _previewSlides[Math.Clamp(_previewSlideIndex, 0, _previewSlides.Count - 1)]
            : _preview?.Content ?? "";

    private void SendLive()
    {
        if (_preview is null) return;
        StopLogoSlideshow();
        if (_preview.Type is ContentType.Image or ContentType.Video or ContentType.Audio &&
            (string.IsNullOrWhiteSpace(_preview.MediaPath) || !File.Exists(_preview.MediaPath)))
        {
            MissingMedia();
            return;
        }

        _blackRestoreSnapshot = null;
        _blackRestoreType = null;
        var liveContent = _preview.Type is ContentType.Image or ContentType.Video or ContentType.Audio or ContentType.Web
            ? _preview.Content
            : CurrentPreviewContent();
        _live = new(PresentationState.Content, _preview.Title, liveContent, _preview.MediaPath);
        _liveType = _preview.Type;
        _preview.Status = "Presentado";
        LiveTitle.Text = "  " + _preview.Title;
        ShowLiveMedia(new ServiceItem
        {
            Type = _preview.Type,
            Title = _preview.Title,
            Content = liveContent,
            MediaPath = _preview.MediaPath
        });
        LiveBadge.Visibility = Visibility.Visible;
        _output?.Render(_live, _preview.Type);
        SetActiveSceneForItem(_preview);
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
        else if (item.Type == ContentType.Audio && File.Exists(item.MediaPath))
        {
            LiveVideo.Source = new Uri(item.MediaPath!);
            LiveVideo.Visibility = Visibility.Visible;
            LiveContent.Visibility = Visibility.Visible;
            LiveContent.Text = "♫\n" + item.Title;
            LiveVideo.Play();
        }
        else if (item.Type == ContentType.Web)
        {
            LiveContent.Visibility = Visibility.Visible;
            LiveContent.Text = "▷ YouTube\n" + item.Title;
        }
        else
        {
            LiveContent.Text = item.Content;
        }
    }

    private void RenderState(PresentationState state, bool preserveBlackRestore = false)
    {
        if (!preserveBlackRestore)
        {
            _blackRestoreSnapshot = null;
            _blackRestoreType = null;
        }

        _live = state switch
        {
            PresentationState.Black => new(state, "Pantalla negra", ""),
            PresentationState.Logo => new(state, "Logotipo", _churchProfile.Name),
            _ => new(PresentationState.Empty, "Sin contenido", "")
        };
        _liveType = null;
        LiveVideo.Stop();
        LiveVideo.Visibility = Visibility.Collapsed;
        LiveImage.Visibility = Visibility.Collapsed;
        LiveContent.Visibility = Visibility.Visible;
        LiveTitle.Text = "  " + _live.Title;
        LiveContent.Text = state == PresentationState.Black
            ? "Pantalla negra"
            : state == PresentationState.Logo
                ? _churchProfile.Name
                : "Sin contenido";
        LiveBadge.Visibility = state == PresentationState.Empty ? Visibility.Collapsed : Visibility.Visible;
        _output?.Render(_live);
        PublishRemoteState();
    }

    private void RestoreFromBlack()
    {
        var snapshot = _blackRestoreSnapshot;
        var type = _blackRestoreType;
        _blackRestoreSnapshot = null;
        _blackRestoreType = null;
        _activeSceneKey = _activeSceneBeforeBlack;
        _activeSceneBeforeBlack = null;
        RefreshSceneRows();

        if (snapshot is null)
        {
            RenderState(PresentationState.Empty);
            return;
        }

        _live = snapshot;
        _liveType = type;
        LiveTitle.Text = "  " + snapshot.Title;
        LiveBadge.Visibility = snapshot.State == PresentationState.Empty ? Visibility.Collapsed : Visibility.Visible;

        if (snapshot.State == PresentationState.Content)
        {
            ShowLiveMedia(new ServiceItem
            {
                Type = type ?? ContentType.FreeText,
                Title = snapshot.Title,
                Content = snapshot.Content,
                MediaPath = snapshot.MediaPath
            });
        }
        else
        {
            LiveVideo.Stop();
            LiveVideo.Visibility = Visibility.Collapsed;
            LiveImage.Visibility = Visibility.Collapsed;
            LiveContent.Visibility = Visibility.Visible;
            LiveContent.Text = snapshot.State == PresentationState.Logo ? "Logotipo de la iglesia" : "Sin contenido";
        }

        _output?.Render(snapshot, type);
        StatusText.Text = "Contenido anterior restaurado";
        PublishRemoteState();
    }

    private void LoadScenes()
    {
        _scenes = _db.ListScenes();
        RefreshSceneRows();
    }

    private void RefreshSceneRows()
    {
        _sceneRows.Clear();
        foreach (var scene in _scenes.OrderBy(x => x.Position).ThenBy(x => x.Id))
            _sceneRows.Add(new SceneRow(scene, string.Equals(scene.Key, _activeSceneKey, StringComparison.OrdinalIgnoreCase)));
        PublishRemoteState();
    }

    private void PrepareSceneFromItem(ServiceItem item)
    {
        var key = SceneKeyForContent(item.Type);
        if (key is null) return;

        try
        {
            _db.UpdateSceneContent(key, item.Title, item.Content, item.MediaPath);
            var scene = _scenes.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (scene is not null)
            {
                scene.Title = item.Title;
                scene.Content = item.Content;
                scene.MediaPath = item.MediaPath;
                scene.UpdatedAt = DateTime.Now;
            }
            RefreshSceneRows();
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo preparar la escena desde el contenido seleccionado", ex);
        }
    }

    private static string? SceneKeyForContent(ContentType type) => type switch
    {
        ContentType.Bible => "bible",
        ContentType.Hymn => "hymn",
        ContentType.Song => "song",
        ContentType.Video => "video",
        ContentType.Web => "youtube",
        ContentType.Welcome or ContentType.FreeText or ContentType.Background => "title",
        _ => null
    };

    private static ContentType ContentTypeForScene(SceneType type) => type switch
    {
        SceneType.Bible => ContentType.Bible,
        SceneType.Hymn => ContentType.Hymn,
        SceneType.Song => ContentType.Song,
        SceneType.Video => ContentType.Video,
        SceneType.YouTube => ContentType.Web,
        SceneType.Image or SceneType.Logo => ContentType.Image,
        SceneType.Audio => ContentType.Audio,
        _ => ContentType.FreeText
    };

    private void SetActiveSceneForItem(ServiceItem item)
    {
        _activeSceneKey = SceneKeyForContent(item.Type);
        RefreshSceneRows();
    }

    private void SceneButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: int id }) return;
        var scene = _scenes.FirstOrDefault(x => x.Id == id);
        if (scene is null) return;
        ActivateScene(scene);
    }

    private void ActivateScene(PresentationScene scene)
    {
        if (scene.Type != SceneType.Logo) StopLogoSlideshow();

        if (scene.Type == SceneType.Black)
        {
            if (_live.State != PresentationState.Black)
            {
                _blackRestoreSnapshot = _live;
                _blackRestoreType = _liveType;
                _activeSceneBeforeBlack = _activeSceneKey;
            }

            _activeSceneKey = scene.Key;
            RenderState(PresentationState.Black, preserveBlackRestore: true);
            RefreshSceneRows();
            StatusText.Text = "Escena Fondo oscuro en vivo";
            return;
        }

        if (scene.Type == SceneType.Logo)
        {
            var settings = ReadLogoSettings(scene);
            var images = settings.ImagePaths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (images.Count > 0)
            {
                StartLogoScene(scene, settings, images);
                return;
            }

            if (string.IsNullOrWhiteSpace(scene.MediaPath))
            {
                StopLogoSlideshow();
                _blackRestoreSnapshot = null;
                _blackRestoreType = null;
                _activeSceneKey = scene.Key;

                if (!string.IsNullOrWhiteSpace(_churchProfile.LogoPath) && File.Exists(_churchProfile.LogoPath))
                {
                    _live = new(PresentationState.Content, _churchProfile.Name, "Logo de la iglesia", _churchProfile.LogoPath);
                    _liveType = ContentType.Image;
                    LiveTitle.Text = "  " + _churchProfile.Name;
                    ShowLiveMedia(new ServiceItem
                    {
                        Type = ContentType.Image,
                        Title = _churchProfile.Name,
                        Content = "Logo de la iglesia",
                        MediaPath = _churchProfile.LogoPath
                    });
                    LiveBadge.Visibility = Visibility.Visible;
                    _output?.Render(_live, ContentType.Image);
                }
                else
                {
                    RenderState(PresentationState.Logo);
                }

                RefreshSceneRows();
                StatusText.Text = "Escena Logo en vivo";
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(scene.Title) &&
            string.IsNullOrWhiteSpace(scene.Content) &&
            string.IsNullOrWhiteSpace(scene.MediaPath))
        {
            OpenModuleForScene(scene);
            return;
        }

        var type = ContentTypeForScene(scene.Type);
        if (type is ContentType.Image or ContentType.Video or ContentType.Audio)
        {
            if (string.IsNullOrWhiteSpace(scene.MediaPath) || !File.Exists(scene.MediaPath))
            {
                StatusText.Text = "La escena no tiene un archivo multimedia disponible";
                OpenModuleForScene(scene);
                return;
            }
        }

        var content = scene.Content;
        if (type is not (ContentType.Image or ContentType.Video or ContentType.Audio or ContentType.Web))
        {
            var slides = TextPaginator.Split(content, 220);
            if (slides.Count > 0) content = slides[0];
        }

        _blackRestoreSnapshot = null;
        _blackRestoreType = null;
        _activeSceneKey = scene.Key;
        _live = new(PresentationState.Content, scene.Title, content, scene.MediaPath);
        _liveType = type;

        LiveTitle.Text = "  " + (string.IsNullOrWhiteSpace(scene.Title) ? scene.Name : scene.Title);
        ShowLiveMedia(new ServiceItem
        {
            Type = type,
            Title = scene.Title,
            Content = content,
            MediaPath = scene.MediaPath
        });

        LiveBadge.Visibility = Visibility.Visible;
        _output?.Render(_live, type);
        RefreshSceneRows();
        StatusText.Text = $"Escena {scene.Name} en vivo";
    }

    private void OpenModuleForScene(PresentationScene scene)
    {
        switch (scene.Type)
        {
            case SceneType.Bible:
                ConfigureMode("Bible");
                break;
            case SceneType.Hymn:
                ConfigureMode("Hymn");
                break;
            case SceneType.Song:
                ConfigureMode("Song");
                break;
            case SceneType.Video:
            case SceneType.Image:
            case SceneType.Audio:
            case SceneType.Logo:
                ConfigureMode("Media");
                break;
            case SceneType.YouTube:
                ConfigureMode("YouTube");
                break;
            case SceneType.Title:
            case SceneType.Custom:
                ConfigureMode("Design");
                break;
            default:
                break;
        }

        StatusText.Text = $"Prepara contenido para la escena {scene.Name}";
    }

    private LogoSceneSettings ReadLogoSettings(PresentationScene scene)
    {
        try
        {
            return JsonSerializer.Deserialize<LogoSceneSettings>(scene.SettingsJson) ?? new LogoSceneSettings();
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo leer la configuración de la escena Logo", ex);
            return new LogoSceneSettings();
        }
    }

    private void AddSelectedMediaToLogo()
    {
        var paths = LibraryList.SelectedItems
            .OfType<LibraryRow>()
            .Select(x => x.Source)
            .OfType<FileSystemEntry>()
            .Where(x => !x.IsFolder && string.Equals(x.Kind, "Imagen", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Path)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0)
        {
            StatusText.Text = "Selecciona una o varias imágenes para agregarlas a Logo";
            return;
        }

        AddImagesToLogo(paths);
    }

    private void AddImagesToLogo(IEnumerable<string> imagePaths)
    {
        var paths = imagePaths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count == 0) return;

        var logo = _scenes.FirstOrDefault(x => x.Type == SceneType.Logo);
        if (logo is null) return;

        var settings = ReadLogoSettings(logo);
        foreach (var path in paths)
        {
            if (!settings.ImagePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                settings.ImagePaths.Add(path);
        }

        logo.SettingsJson = JsonSerializer.Serialize(settings);
        logo.Title = "Presentación de Logo";
        logo.Content = $"{settings.ImagePaths.Count} imágenes";
        logo.MediaPath = settings.ImagePaths.FirstOrDefault(File.Exists);
        _db.SaveScene(logo);
        LoadScenes();

        if (logo.MediaPath is not null)
        {
            ShowPreview(new ServiceItem
            {
                Type = ContentType.Image,
                Title = logo.Title,
                Content = logo.Content,
                MediaPath = logo.MediaPath,
                Status = "Preparado"
            });
        }

        StatusText.Text = paths.Count == 1
            ? "Imagen agregada a la escena Logo"
            : $"{paths.Count} imágenes agregadas a la escena Logo";
    }

    private void StartLogoScene(PresentationScene scene, LogoSceneSettings settings, List<string> images)
    {
        _logoPlaylist = images;
        _logoIndex = 0;
        _logoLoop = settings.Loop;
        _logoTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(settings.IntervalSeconds, 2, 120));

        _blackRestoreSnapshot = null;
        _blackRestoreType = null;
        _activeSceneKey = scene.Key;
        ShowLogoFrame(scene);

        if (_logoPlaylist.Count > 1)
            _logoTimer.Start();

        RefreshSceneRows();
        StatusText.Text = $"Escena Logo en vivo · {_logoPlaylist.Count} imágenes";
    }

    private void ShowLogoFrame(PresentationScene scene)
    {
        if (_logoPlaylist.Count == 0) return;
        _logoIndex = Math.Clamp(_logoIndex, 0, _logoPlaylist.Count - 1);
        var path = _logoPlaylist[_logoIndex];

        _live = new(
            PresentationState.Content,
            scene.Name,
            $"Imagen {_logoIndex + 1} de {_logoPlaylist.Count}",
            path);
        _liveType = ContentType.Image;

        LiveTitle.Text = $"  {scene.Name} · {_logoIndex + 1}/{_logoPlaylist.Count}";
        ShowLiveMedia(new ServiceItem
        {
            Type = ContentType.Image,
            Title = scene.Name,
            Content = _live.Content,
            MediaPath = path
        });
        LiveBadge.Visibility = Visibility.Visible;
        _output?.Render(_live, ContentType.Image);
    }

    private void LogoTimer_Tick(object? sender, EventArgs e)
    {
        if (_logoPlaylist.Count <= 1)
        {
            _logoTimer.Stop();
            return;
        }

        var next = _logoIndex + 1;
        if (next >= _logoPlaylist.Count)
        {
            if (!_logoLoop)
            {
                _logoTimer.Stop();
                return;
            }
            next = 0;
        }

        _logoIndex = next;
        var scene = _scenes.FirstOrDefault(x => x.Type == SceneType.Logo);
        if (scene is not null) ShowLogoFrame(scene);
    }

    private void StopLogoSlideshow()
    {
        _logoTimer.Stop();
        _logoPlaylist = [];
        _logoIndex = 0;
    }

    private void AddScene_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TextEditorWindow(
            "Nueva escena",
            "Nueva escena",
            "",
            false,
            "Nombre de la escena")
        {
            Owner = this
        };

        if (editor.ShowDialog() != true) return;

        var scene = new PresentationScene
        {
            Name = editor.ValueTitle,
            Type = SceneType.Custom,
            Position = _scenes.Count == 0 ? 0 : _scenes.Max(x => x.Position) + 1,
            IsBuiltIn = false
        };

        if (_preview is not null)
        {
            scene.Title = _preview.Title;
            scene.Content = _preview.Content;
            scene.MediaPath = _preview.MediaPath;
            scene.Type = _preview.Type switch
            {
                ContentType.Bible => SceneType.Bible,
                ContentType.Hymn => SceneType.Hymn,
                ContentType.Song => SceneType.Song,
                ContentType.Video => SceneType.Video,
                ContentType.Image => SceneType.Image,
                ContentType.Audio => SceneType.Audio,
                ContentType.Web => SceneType.YouTube,
                _ => SceneType.Custom
            };
        }

        _db.SaveScene(scene);
        LoadScenes();
        StatusText.Text = $"Escena {scene.Name} creada";
    }

    private void ManageScenes_Click(object sender, RoutedEventArgs e)
    {
        var manager = new SceneManagerWindow(_db) { Owner = this };
        manager.ShowDialog();
        LoadScenes();
        StatusText.Text = "Configuración de escenas actualizada";
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsLoaded) LoadLibrary();
    }

    private void OrderNav_Click(object sender, RoutedEventArgs e)
    {
        if (OrderPanel.Visibility == Visibility.Visible)
        {
            OrderPanel.Visibility = Visibility.Collapsed;
            LibraryPanel.Visibility = Visibility.Visible;
            UpdateNavSelection(_mode);
            StatusText.Text = "Volviste al panel anterior";
            return;
        }

        LibraryPanel.Visibility = Visibility.Collapsed;
        OrderPanel.Visibility = Visibility.Visible;
        UpdateNavSelection("Order");
        StatusText.Text = "Orden del culto";
    }

    private void BibleNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Bible");
    private void HymnNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Hymn");
    private void SongsNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Song");
    private void MediaNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Media");
    private void YouTubeNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("YouTube");
    private void DesignsNav_Click(object sender, RoutedEventArgs e) => ConfigureMode("Design");
    private void Services_Click(object sender, RoutedEventArgs e) => ConfigureMode("Services");
    private void Settings_Click(object sender, RoutedEventArgs e) => ConfigureMode("Settings");

    private void MenuNewService_Click(object sender, RoutedEventArgs e) => CreateNewService();

    private void MenuServices_Click(object sender, RoutedEventArgs e) => ConfigureMode("Services");

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    private void IdentifyDisplays_Click(object sender, RoutedEventArgs e)
    {
        _displayManager.IdentifyScreens();
        StatusText.Text = "Identificando pantallas";
    }

    private void ToggleMonitorPanel_Click(object sender, RoutedEventArgs e)
    {
        SetMonitorPanelVisibility(MonitorPanel.Visibility != Visibility.Visible);
    }

    private void SetMonitorPanelVisibility(bool visible)
    {
        MonitorPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        MonitorColumn.MinWidth = visible ? 400 : 0;
        MonitorColumn.MaxWidth = visible ? 480 : 0;
        MonitorColumn.Width = visible ? new GridLength(450) : new GridLength(0);
        MonitorPanelMenuItem.IsChecked = visible;
        _settings.MonitorPanelVisible = visible;

        if (IsLoaded)
        {
            _settingsStore.Save(_settings);
            StatusText.Text = visible
                ? "Monitores de presentación visibles"
                : "Monitores de presentación ocultos";
        }
    }

    private void ShowShortcuts_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Atajos de Cultos\n\n" +
            "← / →   Navegar\n" +
            "Espacio   Enviar en vivo\n" +
            "B   Pantalla negra / restaurar\n" +
            "C   Limpiar salida\n" +
            "F5   Abrir pantalla externa\n" +
            "Esc   Restaurar desde pantalla negra",
            "Atajos de teclado",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.2.0";
        MessageBox.Show(
            $"Cultos {version}\nProducción visual para cultos en Windows.\nFunciona completamente sin conexión.",
            "Acerca de Cultos",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void LoadPreparedYouTubeScene()
    {
        var scene = _scenes.FirstOrDefault(x => x.Type == SceneType.YouTube);
        if (scene is null || string.IsNullOrWhiteSpace(scene.Content)) return;

        YouTubeTitleBox.Text = string.IsNullOrWhiteSpace(scene.Title) ? "YouTube" : scene.Title;
        YouTubeUrlBox.Text = scene.Content;

        if (YouTubeUrlHelper.TryBuildEmbedUrl(scene.Content, false, out var previewUrl))
            YouTubeBrowser.Source = new Uri(previewUrl);
    }

    private void PrepareYouTube_Click(object sender, RoutedEventArgs e)
    {
        PrepareYouTube();
    }

    private bool PrepareYouTube()
    {
        var input = YouTubeUrlBox.Text?.Trim() ?? "";
        if (!YouTubeUrlHelper.TryBuildEmbedUrl(input, false, out var previewUrl) ||
            !YouTubeUrlHelper.TryBuildEmbedUrl(input, true, out var liveUrl))
        {
            StatusText.Text = "El enlace de YouTube no es válido";
            MessageBox.Show(
                "Pega un enlace válido de YouTube, youtu.be o Shorts.",
                "YouTube",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        var title = string.IsNullOrWhiteSpace(YouTubeTitleBox.Text)
            ? "YouTube"
            : YouTubeTitleBox.Text.Trim();

        YouTubeBrowser.Source = new Uri(previewUrl);

        ShowPreview(new ServiceItem
        {
            Type = ContentType.Web,
            Title = title,
            Content = liveUrl,
            Status = "Preparado"
        });

        StatusText.Text = "YouTube preparado en su escena";
        return true;
    }

    private void SendYouTubeLive_Click(object sender, RoutedEventArgs e)
    {
        if (_preview?.Type != ContentType.Web && !PrepareYouTube()) return;
        SendLive();
    }

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
                var supported = SupportedMediaExtensions;
                entries.AddRange(Directory.EnumerateFiles(_currentMediaFolder)
                    .Where(p => supported.Contains(Path.GetExtension(p)))
                    .Select(p =>
                    {
                        var extension = Path.GetExtension(p);
                        var audio = new[] { ".mp3", ".wav", ".m4a", ".aac", ".wma", ".flac" }
                            .Contains(extension, StringComparer.OrdinalIgnoreCase);
                        var video = new[] { ".mp4", ".wmv", ".avi", ".mov", ".mkv" }
                            .Contains(extension, StringComparer.OrdinalIgnoreCase);
                        var kind = audio ? "Audio" : video ? "Video" : "Imagen";
                        return new FileSystemEntry(Path.GetFileName(p), p, false, kind, new FileInfo(p).Length);
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

    private void LibraryList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _libraryDragStart = e.GetPosition(LibraryList);
    }

    private void LibraryList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var current = e.GetPosition(LibraryList);
        if (Math.Abs(current.X - _libraryDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _libraryDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (LibraryList.SelectedItem is not LibraryRow row) return;

        var data = new System.Windows.DataObject();
        data.SetData("Cultos.LibraryRow", row);
        System.Windows.DragDrop.DoDragDrop(LibraryList, data, System.Windows.DragDropEffects.Copy);
    }

    private void Scene_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = System.Windows.DragDropEffects.None;
        if (sender is not System.Windows.Controls.Button { Tag: int }) return;

        if (e.Data.GetDataPresent("Cultos.LibraryRow") ||
            e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            e.Effects = System.Windows.DragDropEffects.Copy;

        e.Handled = true;
    }

    private void Scene_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: int id }) return;
        var scene = _scenes.FirstOrDefault(x => x.Id == id);
        if (scene is null) return;

        if (e.Data.GetData("Cultos.LibraryRow") is LibraryRow row)
        {
            var item = CreateServiceItem(row.Source);
            if (item is not null) AssignItemToScene(scene, item);
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
        {
            if (scene.Type == SceneType.Logo)
            {
                var images = files
                    .Where(File.Exists)
                    .Where(path => new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif" }
                        .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (images.Count > 0)
                    AddImagesToLogo(images);

                e.Handled = true;
                return;
            }

            var item = files.Select(CreateServiceItemFromPath).FirstOrDefault(x => x is not null);
            if (item is not null) AssignItemToScene(scene, item);
            e.Handled = true;
        }
    }

    private void AssignItemToScene(PresentationScene scene, ServiceItem item)
    {
        if (scene.Type == SceneType.Black)
        {
            StatusText.Text = "Fondo oscuro no recibe contenido";
            return;
        }

        if (scene.Type == SceneType.Logo)
        {
            if (item.Type != ContentType.Image || string.IsNullOrWhiteSpace(item.MediaPath))
            {
                StatusText.Text = "La escena Logo solo acepta imágenes";
                return;
            }

            AddImagesToLogo([item.MediaPath]);
            return;
        }

        var compatible = scene.Type switch
        {
            SceneType.Bible => item.Type == ContentType.Bible,
            SceneType.Hymn => item.Type == ContentType.Hymn,
            SceneType.Song => item.Type == ContentType.Song,
            SceneType.Video => item.Type == ContentType.Video,
            SceneType.Audio => item.Type == ContentType.Audio,
            SceneType.YouTube => item.Type == ContentType.Web,
            SceneType.Image => item.Type == ContentType.Image,
            SceneType.Title => item.Type is ContentType.FreeText or ContentType.Welcome or ContentType.Background,
            SceneType.Custom => true,
            _ => true
        };

        if (!compatible)
        {
            StatusText.Text = $"Ese contenido no corresponde a la escena {scene.Name}";
            return;
        }

        if (scene.Type == SceneType.Custom)
        {
            scene.Type = item.Type switch
            {
                ContentType.Bible => SceneType.Bible,
                ContentType.Hymn => SceneType.Hymn,
                ContentType.Song => SceneType.Song,
                ContentType.Video => SceneType.Video,
                ContentType.Audio => SceneType.Audio,
                ContentType.Image => SceneType.Image,
                ContentType.Web => SceneType.YouTube,
                _ => SceneType.Custom
            };
        }

        scene.Title = item.Title;
        scene.Content = item.Content;
        scene.MediaPath = item.MediaPath;
        _db.SaveScene(scene);
        LoadScenes();
        ShowPreview(item);
        StatusText.Text = $"{item.Title} asignado a {scene.Name}";
    }

    private void LibraryList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_mode is "Services" or "Settings") return;
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

        if (_mode is "Services" or "Settings")
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
        if (_mode == "Services")
        {
            OpenSelectedService();
            return;
        }

        if (_mode == "Settings")
        {
            if (LibraryList.SelectedItem is not LibraryRow selected) return;

            if (selected.Source is DisplayChoice display)
            {
                try
                {
                    _displayManager.Select(display.DeviceName);
                    _settingsStore.Save(_settings);
                    PlaceOutputOnConfiguredScreen();
                    LoadLibrary();
                    StatusText.Text = "Pantalla de congregación guardada";
                }
                catch (Exception ex)
                {
                    AppLogger.Error("No se pudo seleccionar la pantalla", ex);
                    StatusText.Text = "La pantalla seleccionada ya no está disponible";
                }
                return;
            }

            if (selected.Source is SettingInfo { Key: "profile" })
            {
                ChurchProfile_Click(this, new RoutedEventArgs());
                return;
            }

            if (selected.Source is SettingInfo { Key: "remote" })
            {
                RemoteControl_Click(this, new RoutedEventArgs());
                return;
            }

            if (selected.Source is SettingInfo { Key: "data" })
            {
                OpenDataFolder();
                return;
            }

            if (selected.Source is SettingInfo { Key: "logs" })
            {
                OpenLogsFolder();
                return;
            }

            if (selected.Source is SettingInfo { Key: "identify" })
            {
                _displayManager.IdentifyScreens();
                StatusText.Text = "Identificando pantallas";
                return;
            }

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
        else if (_mode == "Media") AddSelectedMediaToLogo();
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
        OpenFolder(_dataFolder, "Carpeta de datos abierta");
    }

    private void OpenLogsFolder()
    {
        OpenFolder(Path.Combine(_dataFolder, "logs"), "Carpeta de registros abierta");
    }

    private void OpenFolder(string path, string successMessage)
    {
        Directory.CreateDirectory(path);
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            StatusText.Text = successMessage;
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo abrir una carpeta de la aplicación", ex);
            MessageBox.Show("No se pudo abrir la carpeta. " + ex.Message, "Cultos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearPreview()
    {
        _preview = null;
        _previewSlides = Array.Empty<string>();
        _previewSlideIndex = 0;
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
        var mediaOnly = item.Type is ContentType.Image or ContentType.Video or ContentType.Audio;
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
        if (_previewSlides.Count > 1 && _previewSlideIndex > 0)
        {
            _previewSlideIndex--;
            RenderPreviewSlide();
            return;
        }

        if (RunList.SelectedIndex > 0) RunList.SelectedIndex--;
        else if (RunList.SelectedIndex < 0 && _run.Count > 0) RunList.SelectedIndex = 0;
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_previewSlides.Count > 1 && _previewSlideIndex < _previewSlides.Count - 1)
        {
            _previewSlideIndex++;
            RenderPreviewSlide();
            return;
        }

        if (RunList.SelectedIndex < 0 && _run.Count > 0) RunList.SelectedIndex = 0;
        else if (RunList.SelectedIndex < _run.Count - 1) RunList.SelectedIndex++;
    }

    private void SendLive_Click(object sender, RoutedEventArgs e) => SendLive();

    private void Black_Click(object sender, RoutedEventArgs e)
    {
        if (_live.State == PresentationState.Black)
        {
            RestoreFromBlack();
            return;
        }

        _blackRestoreSnapshot = _live;
        _blackRestoreType = _liveType;
        _activeSceneBeforeBlack = _activeSceneKey;
        _activeSceneKey = "black";
        RenderState(PresentationState.Black, preserveBlackRestore: true);
        RefreshSceneRows();
        StatusText.Text = "Pantalla negra · pulsa B para restaurar";
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => RenderState(PresentationState.Empty);

    private void RemoveText_Click(object sender, RoutedEventArgs e)
    {
        if (_liveType is ContentType.Image or ContentType.Video) return;
        _live = new(PresentationState.Content, _live.Title, "", _live.MediaPath);
        LiveContent.Text = "";
        _output?.Render(_live, _liveType);
        StatusText.Text = "Texto retirado de la salida";
    }

    private void Logo_Click(object sender, RoutedEventArgs e)
    {
        var logo = _scenes.FirstOrDefault(x => x.Type == SceneType.Logo);
        if (logo is not null) ActivateScene(logo);
        else RenderState(PresentationState.Logo);
    }

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
        MediaProgressSlider.Value = 0;
        MediaTimeText.Text = "0:00 / 0:00";
        StatusText.Text = "Video detenido";
    }

    private void LiveVideo_MediaOpened(object sender, RoutedEventArgs e)
    {
        UpdateMediaTimeline();
        StatusText.Text = "Video listo";
    }

    private void MediaTimer_Tick(object? sender, EventArgs e) => UpdateMediaTimeline();

    private void UpdateMediaTimeline()
    {
        if (LiveVideo.Visibility != Visibility.Visible || !LiveVideo.NaturalDuration.HasTimeSpan)
        {
            if (LiveVideo.Visibility != Visibility.Visible)
            {
                MediaProgressSlider.Maximum = 1;
                MediaProgressSlider.Value = 0;
                MediaTimeText.Text = "0:00 / 0:00";
            }
            return;
        }

        var duration = LiveVideo.NaturalDuration.TimeSpan;
        var totalSeconds = Math.Max(1, duration.TotalSeconds);
        MediaProgressSlider.Maximum = totalSeconds;
        MediaProgressSlider.Value = Math.Clamp(LiveVideo.Position.TotalSeconds, 0, totalSeconds);
        MediaTimeText.Text = $"{FormatMediaTime(LiveVideo.Position)} / {FormatMediaTime(duration)}";
    }

    private static string FormatMediaTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    private void MediaProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (LiveVideo.Visibility != Visibility.Visible || !LiveVideo.NaturalDuration.HasTimeSpan) return;
        var position = TimeSpan.FromSeconds(Math.Clamp(MediaProgressSlider.Value, 0, LiveVideo.NaturalDuration.TimeSpan.TotalSeconds));
        LiveVideo.Position = position;
        _output?.SetPosition(position);
        UpdateMediaTimeline();
    }

    private void ApplyMediaVolume()
    {
        var volume = Math.Clamp(MediaVolumeSlider.Value, 0, 1);
        PreviewVideo.Volume = volume;
        LiveVideo.Volume = volume;
        _output?.SetVolume(volume);
    }

    private void MediaVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        _settings.MediaVolume = Math.Clamp(e.NewValue, 0, 1);
        ApplyMediaVolume();
        _settingsStore.Save(_settings);
        StatusText.Text = $"Volumen de video · {Math.Round(_settings.MediaVolume * 100)}%";
    }

    private void PreviewVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        AppLogger.Error("Error al cargar video en vista previa", e.ErrorException);
        ResetPreviewMedia();
        PreviewContent.Text = "No se pudo abrir este video. Puede faltar un códec compatible en Windows.";
        StatusText.Text = "Video incompatible o dañado";
    }

    private void LiveVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        AppLogger.Error("Error al reproducir video en el monitor del operador", e.ErrorException);
        LiveVideo.Visibility = Visibility.Collapsed;
        LiveContent.Visibility = Visibility.Visible;
        LiveContent.Text = "Error de reproducción de video";
        StatusText.Text = "No se pudo reproducir el video";
    }

    private void LiveVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Video finalizado";
    }

    private void OpenDisplay_Click(object sender, RoutedEventArgs e) => OpenDisplay();

    private void OpenDisplay()
    {
        if (_output is null)
        {
            _output = new OutputWindow();
            _output.Closed += (_, _) =>
            {
                _output = null;
                DisplayStateText.Text = "Pantalla cerrada";
                StatusText.Text = "Salida externa cerrada";
            };
            _output.MediaError += (_, message) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    StatusText.Text = "Error de video · " + message;
                });
            };
            _output.MediaEnded += (_, _) => Dispatcher.BeginInvoke(() => StatusText.Text = "Video finalizado");
        }

        PlaceOutputOnConfiguredScreen();
        _output.SetVolume(_settings.MediaVolume);
        _output.SetDefaultBackground(_churchProfile.DefaultBackgroundPath);
        _output.Render(_live, _liveType);
    }

    private void PlaceOutputOnConfiguredScreen()
    {
        if (_output is null) return;

        var target = _displayManager.ResolvePresentationScreen();
        var hasExternalDisplay = Forms.Screen.AllScreens.Length > 1;

        if (hasExternalDisplay)
            _displayManager.PlacePresentationWindow(_output, target);
        else
            _displayManager.PlaceTestWindow(_output, target);

        DisplayStateText.Text = hasExternalDisplay
            ? $"Salida · {target.DeviceName}"
            : "Modo de prueba · una pantalla";
        StatusText.Text = hasExternalDisplay
            ? $"Salida preparada en {target.DeviceName}"
            : "Salida abierta en modo de prueba";
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var configuredAvailable = _displayManager.IsConfiguredScreenAvailable();
            if (_output is not null) PlaceOutputOnConfiguredScreen();
            if (_mode == "Settings") LoadLibrary();

            StatusText.Text = configuredAvailable
                ? "Configuración de pantallas actualizada"
                : "La pantalla configurada se desconectó · usando una pantalla disponible";
        });
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
        OpenCultosFile(dialog.FileName);
    }

    public void OpenExternalFile(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".cultos", StringComparison.OrdinalIgnoreCase))
        {
            OpenCultosFile(path);
            return;
        }

        if (string.Equals(extension, ".cultosperfil", StringComparison.OrdinalIgnoreCase))
        {
            ImportChurchProfileFile(path);
            return;
        }

        MessageBox.Show(
            "Cultos no reconoce este tipo de archivo.",
            "Abrir archivo",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ImportChurchProfileFile(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show("El perfil seleccionado no existe.", "Cultos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(
                "Importar este perfil reemplazará las escenas personalizadas y la identidad visual actual. ¿Continuar?",
                "Importar perfil de iglesia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            StopLogoSlideshow();
            var result = _profileStore.ImportPackage(path);
            _db.ImportSceneProfile(result.Scenes);
            _profileStore.Save(result.Profile);
            _churchProfile = result.Profile;
            ApplyChurchProfile();
            LoadScenes();
            if (_mode == "Settings") LoadLibrary();
            PublishRemoteState();
            StatusText.Text = $"Perfil de {_churchProfile.Name} importado";
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo importar un archivo .cultosperfil", ex);
            MessageBox.Show(
                "No se pudo importar el perfil. " + ex.Message,
                "Importación fallida",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenCultosFile(string path)
    {
        try
        {
            if (!File.Exists(path)) throw new FileNotFoundException("La copia seleccionada no existe.", path);
            if (!string.Equals(Path.GetExtension(path), ".cultos", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El archivo no es una copia de Cultos.");

            SaveNow();
            _service = _db.ImportService(path);
            ServiceNameText.Text = _service.Name;
            RefreshRun();
            ClearPreview();
            ConfigureMode("Services");
            StatusText.Text = "Copia importada correctamente";
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo abrir una copia .cultos", ex);
            MessageBox.Show("No se pudo importar la copia. " + ex.Message, "Importación fallida", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = System.Windows.DragDropEffects.None;
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            e.Handled = true;
            return;
        }

        if (files.Any(path =>
            string.Equals(Path.GetExtension(path), ".cultos", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(path), ".cultosperfil", StringComparison.OrdinalIgnoreCase) ||
            SupportedMediaExtensions.Contains(Path.GetExtension(path))))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }

        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files) return;

        var cultosFile = files.FirstOrDefault(path =>
            string.Equals(Path.GetExtension(path), ".cultos", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(path), ".cultosperfil", StringComparison.OrdinalIgnoreCase));
        if (cultosFile is not null)
        {
            OpenExternalFile(cultosFile);
            return;
        }

        var added = 0;
        foreach (var path in files.Where(path => SupportedMediaExtensions.Contains(Path.GetExtension(path))))
        {
            try
            {
                var media = _db.AddMedia(path);
                _service.Items.Add(new ServiceItem
                {
                    Type = media.Kind switch
                    {
                        "Image" => ContentType.Image,
                        "Audio" => ContentType.Audio,
                        _ => ContentType.Video
                    },
                    Title = media.Name,
                    Content = media.Kind == "Image" ? "Imagen" : "Video",
                    MediaPath = media.Path,
                    Status = "Preparado"
                });
                added++;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"No se pudo agregar el archivo arrastrado: {path}", ex);
            }
        }

        if (added <= 0) return;
        RefreshRun();
        RunList.SelectedIndex = _run.Count - 1;
        QueueSave();
        StatusText.Text = added == 1 ? "Archivo agregado al orden" : $"{added} archivos agregados al orden";
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
        else if (e.Key == Key.Escape && _live.State == PresentationState.Black) RestoreFromBlack();
    }
}

public sealed record LibraryRow(string Title, string Subtitle, object Source);
public sealed record FileSystemEntry(string Name, string Path, bool IsFolder, string Kind, long Size = 0);
public sealed record SettingInfo(string Key);
public sealed record DisplayChoice(string DeviceName);
public sealed record DesignPreset(string Title, string Content, ContentType Type);

public sealed class SceneRow
{
    public SceneRow(PresentationScene scene, bool isLive)
    {
        Scene = scene;
        IsLive = isLive;
    }

    public PresentationScene Scene { get; }
    public int Id => Scene.Id;
    public string Name => Scene.Name;
    public bool IsLive { get; }
    public string Icon => Scene.Type switch
    {
        SceneType.Logo => "◇",
        SceneType.Bible => "▤",
        SceneType.Hymn => "♫",
        SceneType.Song => "♪",
        SceneType.Video => "▶",
        SceneType.YouTube => "▷",
        SceneType.Title => "T",
        SceneType.Black => "■",
        SceneType.Image => "▧",
        SceneType.Audio => "🔊",
        _ => "◆"
    };

    public string StateLabel => Scene.Type switch
    {
        SceneType.Black => "Salida inmediata",
        SceneType.Logo when string.IsNullOrWhiteSpace(Scene.MediaPath) => "Predeterminada",
        SceneType.YouTube when string.IsNullOrWhiteSpace(Scene.Content) => "Sin configurar",
        _ when string.IsNullOrWhiteSpace(Scene.Title) &&
               string.IsNullOrWhiteSpace(Scene.Content) &&
               string.IsNullOrWhiteSpace(Scene.MediaPath) => "Sin preparar",
        _ => "Preparada"
    };

    public string ToolTip => IsLive
        ? $"{Scene.Name} · EN VIVO"
        : $"{Scene.Name} · clic para enviar en vivo";
}

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
        ContentType.Audio => "Audio",
        ContentType.Web => "YouTube",
        ContentType.Welcome => "Bienvenida",
        ContentType.Background => "Fondo",
        _ => "Texto libre"
    };
}
