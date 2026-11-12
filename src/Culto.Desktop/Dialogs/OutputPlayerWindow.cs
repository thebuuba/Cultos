using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

namespace Culto.Desktop.Dialogs;

public sealed record OutputPlayerState(string Title, bool CanPause, bool CanSeek, bool Paused,
    bool Capture, double Position, double Duration, double Volume, bool Loop);

// A view of the existing compositor, never another media player or capture worker.
public sealed class OutputPlayerWindow : Window
{
    public OutputMonitor Monitor { get; } = new();
    public event Action? PauseRequested, RestartRequested, MuteRequested, ReturnMediaRequested;
    public event Action<double>? SeekRequested, SkipRequested, VolumeRequested;
    public event Action<bool>? LoopRequested;
    private readonly Slider _position = new() { Minimum = 0, Maximum = 1, IsMoveToPointEnabled = true,
        SmallChange = .01, LargeChange = .1, Margin = new Thickness(0, 4, 0, 10) };
    private readonly Slider _volume = new() { Minimum = 0, Maximum = 100, Width = 110,
        IsMoveToPointEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
    private readonly TextBlock _time = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 14, 0) };
    private readonly CheckBox _loop = new() { Content = "Repetir", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
    private readonly CheckBox _pin = new() { Content = "Fijar", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
    private readonly Button _pause, _restart, _back, _forward, _mute, _fullscreen;
    private bool _updating, _scrubbing, _isFullscreen;
    private Rect _restoreBounds;
    private WindowState _restoreState;
    private OutputPlayerState? _state;

    public OutputPlayerWindow()
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Reproductor de salida";
        Width = Math.Min(1000, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(650, SystemParameters.WorkArea.Height - 40);
        MinWidth = 480; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanResize;
        NameScope.SetNameScope(this, new NameScope());
        Register("PlayerMonitor", Monitor);
        Register("PlayerPosition", _position); Register("PlayerVolume", _volume);
        AutomationProperties.SetName(_position, "Posición de la salida");
        AutomationProperties.SetName(_volume, "Volumen de la salida");
        _pause = CreateIconButton("PlayerPause", "\uE768", "Reproducir salida", () => PauseRequested?.Invoke());
        _restart = CreateIconButton("PlayerRestart", "\uE72C", "Reiniciar", () => RestartRequested?.Invoke());
        _back = CreateIconButton("PlayerBack", "\uE892", "Retroceder 10 segundos", () => SkipRequested?.Invoke(-10));
        _forward = CreateIconButton("PlayerForward", "\uE893", "Avanzar 10 segundos", () => SkipRequested?.Invoke(10));
        _mute = CreateIconButton("PlayerMute", "\uE767", "Silenciar salida", () => MuteRequested?.Invoke());
        _fullscreen = CreateIconButton("PlayerFullscreen", "\uE740", "Pantalla completa · F11", ToggleFullscreen);
        Register("PlayerLoop", _loop); Register("PlayerPin", _pin);
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var image = new Border { Background = Brushes.Black, Child = Monitor, ClipToBounds = true };
        image.MouseLeftButtonDown += (_, e) =>
        { if (e.ClickCount == 2) { ToggleFullscreen(); e.Handled = true; } };
        root.Children.Add(image);
        var controls = new StackPanel { Margin = new Thickness(16, 8, 16, 12) };
        controls.Children.Add(_position);
        var transport = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        foreach (var control in new UIElement[] { _restart, _back, _pause, _forward, _time, _mute, _volume, _loop, _pin, _fullscreen })
            transport.Children.Add(control);
        controls.Children.Add(transport);
        Grid.SetRow(controls, 1); root.Children.Add(controls);
        Content = root;
        _volume.ValueChanged += (_, _) => { if (!_updating) VolumeRequested?.Invoke(_volume.Value); };
        _loop.Checked += (_, _) => { if (!_updating) LoopRequested?.Invoke(true); };
        _loop.Unchecked += (_, _) => { if (!_updating) LoopRequested?.Invoke(false); };
        _pin.Checked += (_, _) => Topmost = true;
        _pin.Unchecked += (_, _) => Topmost = _isFullscreen;
        WireSeek();
        PreviewKeyDown += PlayerKeyDown;
        Closed += (_, _) => Monitor.Dispose();
        Update(new("Sin contenido", false, false, true, false, 0, 0, 80, false));
    }

    private void Register(string name, FrameworkElement element) { element.Name = name; RegisterName(name, element); }
    private Button CreateIconButton(string name, string glyph, string label, Action action)
    {
        var button = new Button { Content = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 17,
            MinWidth = 38, MinHeight = 38, Padding = new Thickness(9), Margin = new Thickness(2), ToolTip = label };
        Register(name, button); AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action(); return button;
    }

    public void Update(OutputPlayerState state)
    {
        _state = state; _updating = true;
        try
        {
            Title = state.Title + " — Reproductor de salida";
            _pause.IsEnabled = state.CanPause;
            _pause.Visibility = state.Capture ? Visibility.Collapsed : Visibility.Visible;
            _pause.Content = state.Paused ? "\uE768" : "\uE769";
            var pauseLabel = state.Capture ? (state.Paused ? "Continuar captura" : "Congelar captura")
                : state.Paused ? "Reproducir salida" : "Pausar salida";
            _pause.ToolTip = pauseLabel; AutomationProperties.SetName(_pause, pauseLabel);
            _position.IsEnabled = state.CanSeek;
            _restart.IsEnabled = _back.IsEnabled = _forward.IsEnabled = _loop.IsEnabled = state.CanSeek;
            if (!_scrubbing) _position.Value = state.Duration > 0 ? Math.Clamp(state.Position / state.Duration, 0, 1) : 0;
            _time.Text = state.Capture ? "EN DIRECTO" : FormatTime(_scrubbing ? _position.Value * state.Duration : state.Position)
                + " / " + FormatTime(state.Duration);
            _volume.Value = state.Volume; _loop.IsChecked = state.Loop;
            _mute.Content = state.Volume <= 0 ? "\uE74F" : "\uE767";
            var muteLabel = state.Volume <= 0 ? "Restaurar volumen" : "Silenciar salida";
            _mute.ToolTip = muteLabel; AutomationProperties.SetName(_mute, muteLabel);
        }
        finally { _updating = false; }
    }

    private void WireSeek()
    {
        void Commit() { if (_scrubbing && _state?.CanSeek == true) SeekRequested?.Invoke(_position.Value); _scrubbing = false; }
        _position.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) =>
        { _scrubbing = true; if (e.Handled && !_position.IsMouseCaptureWithin) _position.CaptureMouse(); }), true);
        _position.AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) =>
        { Commit(); if (_position.IsMouseCaptured) _position.ReleaseMouseCapture(); }), true);
        _position.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => _scrubbing = true));
        _position.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, e) =>
        { if (!e.Canceled) Commit(); else _scrubbing = false; }));
        _position.LostMouseCapture += (_, _) => _scrubbing = false;
        _position.PreviewKeyDown += (_, e) => { if (IsSeekKey(e.Key)) _scrubbing = true; };
        _position.PreviewKeyUp += (_, e) => { if (IsSeekKey(e.Key)) Commit(); };
        _position.LostKeyboardFocus += (_, _) => _scrubbing = false;
    }

    private static bool IsSeekKey(Key key) => key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;
    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(double.IsFinite(seconds) ? Math.Max(0, seconds) : 0);
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");
    }
    private void PlayerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F9) { ReturnMediaRequested?.Invoke(); e.Handled = true; return; }
        if (e.Key == Key.F11 || (e.Key == Key.Escape && _isFullscreen)) { ToggleFullscreen(); e.Handled = true; return; }
        if (e.OriginalSource is DependencyObject source)
            for (var parent = source; parent is not null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is Slider or ButtonBase) return;
        if (e.Key == Key.Space && _state?.CanPause == true) { PauseRequested?.Invoke(); e.Handled = true; }
        if (e.Key is Key.Left or Key.Right && _state?.CanSeek == true)
        { SkipRequested?.Invoke(e.Key == Key.Left ? -5 : 5); e.Handled = true; }
    }
    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _restoreState = WindowState;
            _restoreBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            _isFullscreen = true;
            var display = DisplayService.GetDisplays().First(d => d.DeviceName == DisplayService.GetWindowDevice(this));
            DisplayService.PlaceWindow(this, display, fullscreen: true);
        }
        else
        {
            _isFullscreen = false;
            WindowState = WindowState.Normal; WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
            Left = _restoreBounds.Left; Top = _restoreBounds.Top; Width = _restoreBounds.Width; Height = _restoreBounds.Height;
            WindowState = _restoreState; Topmost = _pin.IsChecked == true;
        }
        _fullscreen.Content = _isFullscreen ? "\uE73F" : "\uE740";
        var label = _isFullscreen ? "Salir de pantalla completa · Esc" : "Pantalla completa · F11";
        _fullscreen.ToolTip = label; AutomationProperties.SetName(_fullscreen, label);
    }
}
