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
    private bool _previewPaused;
    private bool _isPaused;
    private int _displayTick;
    private Point _dragStart;
    private Cue? _notesCue;

    public MainWindow()
    {
        InitializeComponent();
        InitializePlayerControls();
        InitializeUsabilityShortcuts();
        _plan.CollectionChanged += (_, _) => UpdateUsabilityState();
        Loaded += (_, _) => InitializeWorkspace();
        Closing += OnWindowClosing;
        InputBindings.Add(new KeyBinding(new ActionCommand(ProjectPrepared), Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => MoveLive(1)), Key.F6, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => MoveLive(-1)), Key.F7, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new ActionCommand(ToggleBlackout), Key.F8, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => SavePlanClick(this, new RoutedEventArgs())), Key.S, ModifierKeys.Control));
    }

    private void InitializeWorkspace()
    {
        string? recoveryMessage = null;
        try { _workspace = _store.Load(); }
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
        InitializeDocking();
        _session.Changed += UpdateSessionLabels;
        _ready = true;
        RefreshLibrary();
        LoadSavedBible();
        UpdatePlanCount();
        if (_plan.Count > 0) PlanList.SelectedIndex = 0;
        _timer.Tick += TimerTick;
        _timer.Start();
        if (recoveryMessage is not null) SetStatus(recoveryMessage, true);
    }

    private void PrepareCue(Cue cue, IReadOnlyList<Cue>? reading = null)
    {
        _preparedReading = reading;
        _session.Prepare(cue);
        _preview.ShowCue(cue);
        _previewPaused = false;
        UpdatePlayButton(PreviewPauseButton, false, true);
        PreviewTimeText.Text = "00:00";
        PreviewDurationText.Text = "00:00";
        PreviewPositionSlider.Value = 0;
        PreparedTitleText.Text = cue.Title;
        UpdateUsabilityState();
        SetStatus($"Vista previa: {cue.Title}");
    }

    private void UpdateSessionLabels()
    {
        PreparedTitleText.Text = _session.Prepared?.Title ?? "Sin contenido";
        var live = _output is null ? null : _session.Live;
        ToggleOutputButton.Content = _outputWindow is null ? "Activar HDMI" : "Desactivar HDMI";
        if (_outputWindow is null) OutputStatusText.Text = _output is null ? "Salida desactivada" : "Reproducción local · HDMI desactivado";
        LiveTitleText.Text = live?.Title ?? "Sin contenido";
        LiveReferenceText.Text = live is null ? "" : string.Join(" · ", new[] { live.Reference, live.Version }.Where(s => !string.IsNullOrWhiteSpace(s)));
        LiveBodyText.Text = _output is null ? "La salida está cerrada." : _session.IsBlackout ? "Pantalla en negro" : _session.IsTextHidden ? "Fondo sin contenido" : live?.Kind switch
        {
            CueKind.Video => "VIDEO EN SALIDA\n" + live.Title,
            CueKind.Audio => "AUDIO EN SALIDA\n" + live.Title,
            CueKind.Image => "IMAGEN EN SALIDA\n" + live.Title,
            _ => live?.Body ?? "Salida preparada"
        };
        OutputModeText.Text = _output is null ? "SIN SALIDA"
            : _session.IsBlackout ? "NEGRO"
            : _session.IsTextHidden ? "FONDO"
            : _outputWindow is null ? "LOCAL" : _outputIsTest ? "ENSAYO" : "ACTIVA";
        OutputModeText.Foreground = (Brush)FindResource(_output is not null && (_session.IsBlackout || _session.IsTextHidden) ? "AmberBrush" : "MutedBrush");
        BlackoutButton.Content = "Fondo negro";
        BlackoutButton.ToolTip = _session.IsBlackout ? "Restaurar contenido" : "Fondo negro";
        BlackoutButton.Background = (Brush)FindResource(_session.IsBlackout ? "AmberBrush" : "RaisedBrush");
        BlackoutButton.Foreground = _session.IsBlackout ? Brushes.Black : (Brush)FindResource("TextBrush");
        var churchActive = _output is not null && _session.IsTextHidden && !_session.IsBlackout;
        ChurchBackgroundButton.Background = (Brush)FindResource(churchActive ? "SelectedBrush" : "RaisedBrush");
        ChurchBackgroundButton.Foreground = (Brush)FindResource(churchActive ? "AccentBrush" : "TextBrush");
        ChurchBackgroundButton.ToolTip = "Elegir fondo de iglesia";
        UpdateUsabilityState();
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
        try { _store.Save(_workspace); return true; }
        catch (Exception ex)
        {
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
        CommitNotes();
        _notesCue = PlanList.SelectedItem as Cue;
        NotesBox.Text = _notesCue?.Notes ?? "";
        if (_notesCue is not null) PrepareCue(_notesCue);
    }
    private void CommitNotes()
    {
        if (_notesCue is not null && _notesCue.Notes != NotesBox.Text)
        { _notesCue.Notes = NotesBox.Text; SaveWorkspace(); }
    }
    private void NotesChanged(object sender, RoutedEventArgs e) => CommitNotes();
    private void PreviewPauseClick(object sender, RoutedEventArgs e)
    {
        if (!_ready || _session.Prepared?.Kind is not (CueKind.Video or CueKind.Audio)) return;
        _previewPaused = !_preview.IsPaused; _preview.SetPaused(_previewPaused);
        UpdatePlayButton(PreviewPauseButton, _previewPaused, true);
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;
        e.Cancel = true;
        CommitNotes();
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
        try { if (_remote is not null) await _remote.DisposeAsync(); }
        catch (Exception) { /* Closing must still release the native media players. */ }
        CloseOutput();
        _preview?.Dispose();
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
