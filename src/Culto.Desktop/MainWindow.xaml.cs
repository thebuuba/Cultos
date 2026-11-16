using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;
using Culto.Desktop.Dialogs;
using Culto.Remote;

namespace Culto.Desktop;

public partial class MainWindow : Window
{
    private readonly WorkspaceStore _store = new();
    private WorkspaceData _workspace = new();
    private readonly ObservableCollection<Cue> _plan = [];
    private readonly ObservableCollection<Cue> _libraryView = [];
    private readonly PresentationSession _session = new();
    private ProjectionSurface _preview = null!;
    private ProjectionSurface? _output;
    private ProjectionSurface? _standbyOutput;
    private ProjectionWindow? _outputWindow;
    private DisplayInfo? _outputDisplay;
    private bool _outputIsTest;
    private BibleCatalog? _bible;
    private IReadOnlyList<Cue>? _preparedReading;
    private IReadOnlyList<Cue>? _liveReading;
    private RemoteHost? _remote;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool _ready;
    private bool _closing;
    private bool _workspaceRecoveryBlocked;
    private bool _isPaused;
    private int _displayTick;
    private Point _dragStart;

    public MainWindow()
    {
        InitializeComponent();
        PowerPointPanel.IsVisibleChanged += PowerPointPanelVisibilityChanged;
        InitializeWorkspaceHeader();
        InitializeBibleReferenceInput();
        InitializeOrderActions();
        InitializePlayerControls();
        InitializeUsabilityShortcuts();
        InitializeContentDragDrop();
        _plan.CollectionChanged += (_, _) => { UpdateUsabilityState(); RefreshPowerPointFiles(); };
        Loaded += (_, _) => InitializeWorkspace();
        Closing += OnWindowClosing;
        _browserCaptureResizeTimer.Tick+=RefreshBrowserCaptureAfterResize;
        SizeChanged += (_, _) => BrowserCaptureLayoutChanged();
        StateChanged += (_, _) => BrowserCaptureLayoutChanged();
        Activated += (_, _) => BrowserCaptureActivated();
        Deactivated += (_, _) => BrowserCaptureLayoutChanged();
        InputBindings.Add(new KeyBinding(new ActionCommand(ProjectPrepared), Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => ProjectPreparedPreservingOther()), Key.F5, ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => AdvancePresentation(1)), Key.F6, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => AdvancePresentation(-1)), Key.F7, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new ActionCommand(ToggleBlackout), Key.F8, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => SavePlanClick(this, new RoutedEventArgs())), Key.S, ModifierKeys.Control));
    }

    private void InitializeWorkspace()
    {
        var workspacePath = Path.Combine(_store.DirectoryPath, "workspace.json");
        var hadWorkspace = File.Exists(workspacePath) || File.Exists(workspacePath + ".bak");
        string? recoveryMessage = null;
        try
        {
            _workspace = _store.Load();
            _workspaceSaveOkay = File.Exists(Path.Combine(_store.DirectoryPath, "workspace.json")) ? true : null;
        }
        catch (Exception ex)
        {
            _workspaceRecoveryBlocked = true;
            recoveryMessage = "No se pudo recuperar el culto guardado. Exporta los cambios con Guardar. " + ex.Message;
            if (ex is InvalidDataException)
            {
                try
                {
                    _store.ArchiveDamagedFile("workspace.json");
                    _workspace = _store.Load();
                    _workspaceRecoveryBlocked = false;
                    recoveryMessage = "El culto dañado se conservó en archivos .recovery. Puedes preparar y guardar un culto nuevo.";
                }
                catch (Exception recoveryError)
                { recoveryMessage += " No se pudieron archivar los archivos: " + recoveryError.Message; }
            }
        }
        foreach (var cue in _workspace.Plan.Items) _plan.Add(cue);
        PlanList.ItemsSource = _plan;
        LibraryList.ItemsSource = _libraryView;
        PlanNameBox.Text = _workspace.Plan.Name;
        VolumeSlider.Value = _workspace.Volume;
        LoopCheck.IsChecked = _workspace.Loop;
        _preview = new ProjectionSurface(preview: true);
        _preview.PlaybackError += message => Dispatcher.BeginInvoke(() => SetStatus(message, true));
        PreviewHost.Content = _preview;
        InitializeVideoListsPanel();
        InitializeDocking();
        _session.Changed += UpdateSessionLabels;
        _ready = true;
        InitializeNotes();
        RefreshVideoLists();
        InitializeYouTube();
        InitializeScenes();
        InitializeMediaBrowser();
        InitializeSplitMediaActions();
        SetupPresenterHotkeys();
        RefreshLibrary();
        LoadSavedBible();
        UpdatePlanCount();
        _timer.Tick += TimerTick;
        _timer.Start();
        // Warm native codecs without selecting or starting saved media.
        _standbyOutput = new ProjectionSurface(smoothTransitions: true);
        _standbyOutput.PreparePlaybackEngine();
        ShowStartupLogo();
        if (recoveryMessage is not null) SetStatus(recoveryMessage, true);
        ScheduleFirstRunSetup(hadWorkspace);
    }

    private void PrepareCue(Cue cue, IReadOnlyList<Cue>? reading = null)
    {
        _preparedReading = reading;
        _session.Prepare(cue);
        RenderPreparedPreview();
        PreparedTitleText.Text = cue.Title;
        UpdateUsabilityState();
        SyncPreviewSource();
        UpdateBackgroundAudioState();
        UpdateBrowserCaptureCompatibility();
        RefreshSceneChoices();
        SetStatus($"Vista previa: {cue.Title}");
    }

    private void UpdateSessionLabels()
    {
        SyncLogoPlayback();
        PreparedTitleText.Text = _session.Prepared?.Title ?? "Sin contenido";
        var live = _output is null ? null : _session.Live;
        UpdateOutputSwitchLabel();
        if (_outputWindow is null) OutputStatusText.Text = _output is null ? "Salida desactivada" : "Reproducción local · HDMI desactivado";
        LiveTitleText.Text = _output is not null && _session.IsTextHidden && !_session.IsBlackout ? "Logo" : live?.Title ?? "Sin contenido";
        LiveReferenceText.Text = _session.IsTextHidden ? Path.GetFileNameWithoutExtension(CurrentBackgroundPath) ?? ""
            : live is null ? "" : string.Join(" · ", new[] { live.Reference, live.Version }.Where(s => !string.IsNullOrWhiteSpace(s)));
        LiveBodyText.Text = _output is null ? "La salida está cerrada." : _session.IsBlackout ? "Pantalla en negro" : _session.IsTextHidden ? "Fondo sin contenido" : live?.Kind switch
        {
            CueKind.Video => "VIDEO EN SALIDA\n" + live.Title,
            CueKind.Audio => "AUDIO EN SALIDA\n" + live.Title,
            CueKind.Image => "IMAGEN EN SALIDA\n" + live.Title,
            _ => live?.Body ?? "Salida preparada"
        };
        OutputModeText.Text = _output is null ? "SIN SALIDA"
            : _session.IsBlackout ? "NEGRO"
            : _session.IsTextHidden ? "LOGO"
            : _outputWindow is null ? "LOCAL" : _outputIsTest ? "ENSAYO" : "ACTIVA";
        OutputModeText.Foreground = (Brush)FindResource(_output is not null && (_session.IsBlackout || _session.IsTextHidden) ? "AmberBrush" : "MutedBrush");
        BlackoutButton.ToolTip = _session.IsBlackout ? "Restaurar contenido" : "Fondo negro";
        ChurchBackgroundButton.ToolTip = "Logo";
        UpdateSceneActiveStates();
        RefreshSceneChoices();
        UpdateUsabilityState();
        SyncPreviewSource();
        UpdateBrowserCaptureCompatibility();
    }

    private void SetStatus(string message, bool error = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = error ? new SolidColorBrush(Color.FromRgb(255, 171, 171)) : (Brush)FindResource("MutedBrush");
        StatusText.ToolTip = message;
    }

    private bool SaveWorkspace()
    {
        if (!_ready || _closing) return true;
        _workspace.Plan.Name = string.IsNullOrWhiteSpace(PlanNameBox.Text) ? "Culto sin título" : PlanNameBox.Text.Trim();
        _workspace.Plan.Items = _plan.ToList();
        _workspace.Volume = (int)VolumeSlider.Value;
        _workspace.Loop = LoopCheck.IsChecked == true;
        if (_workspaceRecoveryBlocked)
        {
            SetStatus("No se puede guardar sobre los archivos sin recuperar. Exporta el culto con Guardar antes de cerrar.", true);
            return false;
        }
        try { _store.Save(_workspace); _workspaceSaveOkay = true; return true; }
        catch (Exception ex)
        {
            _workspaceSaveOkay = false;
            SetStatus("No se pudo guardar. Exporta el culto con Guardar. " + ex.Message, true);
            return false;
        }
    }

    private void UpdatePlanCount()
    {
        PlanCountText.Text = _plan.Count.ToString();
        PlanEmptyText.Visibility = _plan.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateUsabilityState();
    }
    private void PlanNameChanged(object sender, RoutedEventArgs e) => SaveWorkspace();
    private void PlanSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;

        if (PlanList.SelectedItem is Cue cue) PrepareCue(cue);
        else FollowLivePreview();
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;
        e.Cancel = true;

        if (!SaveWorkspace())
        {
            if (new UnsavedChangesDialog { Owner = this }.ShowDialog() != true)
            {
                SetStatus("La consola sigue abierta. Usa Guardar para exportar el orden o corrige el almacenamiento antes de cerrar.", true);
                return;
            }
        }
        SaveDockLayout();
        _closing = true; _timer.Stop();
        UpdateBrowserCaptureCompatibility();
        DisposeYouTube();
        DisposePresenterHotkeys();
        _bibleSearchTimer?.Stop();
        DisposeLibraryFolders();
        try { if (_remote is not null) await _remote.DisposeAsync(); }
        catch (Exception) { /* Closing must still release the native media players. */ }
        _outputPlayerWindow?.Close();
        CloseOutput();
        _preview?.Dispose();
        PreviewLiveMonitor.Dispose();
        LiveMonitor.Dispose();
        Application.Current.Shutdown();
    }
}

public sealed class ActionCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
