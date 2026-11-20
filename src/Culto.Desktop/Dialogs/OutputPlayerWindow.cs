using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Data;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

namespace Culto.Desktop.Dialogs;

public sealed record OutputPlayerState(string Title, bool CanPause, bool CanSeek, bool Paused,
    bool Capture, double Position, double Duration, double Volume, bool Loop, bool Preview = false, bool ControlVolume = true);

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
    private readonly Grid _caption = new() { Height = 34 };
    private readonly Border _controls = new() { VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(10, 40, 10, 8) };
    private readonly Popup _volumePopup = new() { Placement = PlacementMode.Top, AllowsTransparency = true, StaysOpen = false };
    private readonly CueThumbnail _cover = new() { Visibility = Visibility.Collapsed };

    public OutputPlayerWindow()
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Reproductor de salida";
        Width = Math.Min(1000, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(650, SystemParameters.WorkArea.Height - 40);
        MinWidth = 480; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 34, ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false
        });
        NameScope.SetNameScope(this, new NameScope());
        Register("PlayerMonitor", Monitor);
        Register("PlayerPosition", _position); Register("PlayerVolume", _volume);
        AutomationProperties.SetName(_position, "Posición de la salida");
        AutomationProperties.SetName(_volume, "Volumen de la salida");
        _pause = CreateIconButton("PlayerPause", "\uE768", "Reproducir salida", () => PauseRequested?.Invoke());
        _restart = CreateIconButton("PlayerRestart", "\uE72C", "Reiniciar", () => RestartRequested?.Invoke());
        _back = CreateIconButton("PlayerBack", "\uE892", "Retroceder 10 segundos", () => SkipRequested?.Invoke(-10));
        _forward = CreateIconButton("PlayerForward", "\uE893", "Avanzar 10 segundos", () => SkipRequested?.Invoke(10));
        _back.Content = LineIcon("M3 11 A9 9 0 1 0 5.7 5.7 L3 8 M3 3V8H8");
        _forward.Content = LineIcon("M21 11 A9 9 0 1 1 18.3 5.7 L21 8 M21 3V8H16");
        _mute = CreateIconButton("PlayerMute", "\uE767", "Silenciar el programa", () => MuteRequested?.Invoke());
        _fullscreen = CreateIconButton("PlayerFullscreen", "\uE740", "Pantalla completa · F11", ToggleFullscreen);
        _fullscreen.Content = LineIcon("M8 3H3V8 M16 3H21V8 M21 16V21H16 M3 16V21H8");
        Register("PlayerLoop", _loop); Register("PlayerPin", _pin);
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        _caption.ColumnDefinitions.Add(new ColumnDefinition());
        _caption.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var captionTitle = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 8, 0), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        captionTitle.SetBinding(TextBlock.TextProperty, new Binding(nameof(Title)) { Source = this });
        _caption.Children.Add(captionTitle);
        var windowActions = new StackPanel { Orientation = Orientation.Horizontal };
        void CaptionButton(string name, string glyph, string label, Action action)
        {
            var button = CreateIconButton(name, glyph, label, action);
            button.MinHeight = 30; button.MinWidth = 40; button.Height = 30;
            button.FontSize = 12; button.Padding = new Thickness(8, 2, 8, 2); button.Margin = new Thickness(1, 2, 1, 2);
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            windowActions.Children.Add(button);
        }
        CaptionButton("PlayerMinimize", "\uE921", "Minimizar", () => WindowState = WindowState.Minimized);
        CaptionButton("PlayerMaximize", "\uE922", "Maximizar o restaurar", () =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
        // WPF handles this button without Windows' blocking native caption loop.
        CaptionButton("PlayerClose", "\uE8BB", "Cerrar reproductor", Close);
        Grid.SetColumn(windowActions, 1); _caption.Children.Add(windowActions);
        root.Children.Add(_caption);
        var imageRoot = new Grid { Background = Brushes.Black, ClipToBounds = true };
        imageRoot.Children.Add(Monitor); imageRoot.Children.Add(_cover);
        var image = new Border { Background = Brushes.Black, Child = imageRoot, ClipToBounds = true };
        image.MouseLeftButtonDown += (_, e) =>
        { if (e.ClickCount == 2) { ToggleFullscreen(); e.Handled = true; } };
        Grid.SetRow(image, 1); root.Children.Add(image);
        _controls.Background = new LinearGradientBrush(new GradientStopCollection {
            new(Color.FromArgb(0,0,0,0),0), new(Color.FromArgb(128,0,0,0),.5), new(Color.FromArgb(217,0,0,0),1) }, new Point(0,0), new Point(0,1));
        Register("PlayerOverlay", _controls);
        var controls = new StackPanel();
        _position.Margin = new Thickness(0); _position.Height = 24;
        _position.Foreground = (Brush)Application.Current.FindResource("AccentBrush");
        _position.Background = new SolidColorBrush(Color.FromArgb(102,95,98,101));
        controls.Children.Add(_position);
        var transport = new DockPanel { Height = 32, LastChildFill = true };
        DockPanel.SetDock(_fullscreen, Dock.Right); transport.Children.Add(_fullscreen);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var control in new UIElement[] { _pause, _back, _forward, _mute, _time }) actions.Children.Add(control);
        transport.Children.Add(actions); controls.Children.Add(transport);
        _time.Foreground = Brushes.White; _time.FontSize = 11; _time.Margin = new Thickness(6,0,0,0);
        _volume.Width = 100; _volume.Margin = new Thickness(0);
        _volume.Foreground = _position.Foreground;
        var volumeHost = new Border { Background = new SolidColorBrush(Color.FromArgb(238,21,22,24)), CornerRadius = new CornerRadius(8), Padding = new Thickness(10,5,10,5), Child = _volume };
        _volumePopup.Child = volumeHost; _volumePopup.PlacementTarget = _mute;
        _mute.MouseEnter += (_, _) => _volumePopup.IsOpen = true;
        _mute.GotKeyboardFocus += (_, _) => _volumePopup.IsOpen = true;
        _mute.PreviewKeyDown += (_, e) => { if (e.Key == Key.Up) { _volumePopup.IsOpen = true; _volume.Focus(); e.Handled = true; } };
        _volumePopup.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { _volumePopup.IsOpen = false; _pause.Focus(); e.Handled = true; } };
        void CloseVolume() => Dispatcher.BeginInvoke(new Action(() => { if (!_mute.IsMouseOver && !volumeHost.IsMouseOver && !_volume.IsKeyboardFocusWithin && !_volume.IsMouseCaptureWithin) _volumePopup.IsOpen = false; }));
        _mute.MouseLeave += (_, _) => CloseVolume(); volumeHost.MouseLeave += (_, _) => CloseVolume();
        _volume.LostKeyboardFocus += (_, _) => CloseVolume();
        controls.Children.Add(_volumePopup); _controls.Child = controls; imageRoot.Children.Add(_controls);
        var menu = new ContextMenu();
        var repeatItem = new MenuItem { Header = "Repetir", IsCheckable = true };
        var pinItem = new MenuItem { Header = "Fijar ventana", IsCheckable = true };
        repeatItem.Click += (_, _) => _loop.IsChecked = repeatItem.IsChecked;
        pinItem.Click += (_, _) => _pin.IsChecked = pinItem.IsChecked;
        var restartItem = new MenuItem { Header = "Volver al inicio" }; restartItem.Click += (_, _) => RestartRequested?.Invoke();
        menu.Items.Add(repeatItem); menu.Items.Add(pinItem); menu.Items.Add(restartItem);
        menu.Opened += (_, _) => { repeatItem.IsChecked = _loop.IsChecked == true; repeatItem.IsEnabled = restartItem.IsEnabled = _state?.CanSeek == true; pinItem.IsChecked = _pin.IsChecked == true; };
        image.ContextMenu = menu;
        Content = root;
        _volume.ValueChanged += (_, _) => { if (!_updating) VolumeRequested?.Invoke(_volume.Value); };
        _loop.Checked += (_, _) => { if (!_updating) LoopRequested?.Invoke(true); };
        _loop.Unchecked += (_, _) => { if (!_updating) LoopRequested?.Invoke(false); };
        _pin.Checked += (_, _) => Topmost = true;
        _pin.Unchecked += (_, _) => Topmost = _isFullscreen;
        WireSeek();
        PreviewKeyDown += PlayerKeyDown;
        Closed += (_, _) => { _volumePopup.IsOpen = false; Monitor.Dispose(); };
        Update(new("Sin contenido", false, false, true, false, 0, 0, 80, false));
    }

    private void Register(string name, FrameworkElement element) { element.Name = name; RegisterName(name, element); }
    private static FrameworkElement LineIcon(string data) => new System.Windows.Shapes.Path
    {
        Data = Geometry.Parse(data), Width = 17, Height = 17, Stretch = Stretch.Uniform, Stroke = Brushes.White,
        StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
    };

    private Button CreateIconButton(string name, string glyph, string label, Action action)
    {
        var button = new Button { Content = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 17,
            Width = 30, Height = 30, MinWidth = 0, MinHeight = 30, Padding = new Thickness(0), Margin = new Thickness(1,0,1,0), ToolTip = label,
            Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Foreground = Brushes.White };
        Register(name, button); AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action(); return button;
    }

    public void Update(OutputPlayerState state)
    {
        _state = state; _updating = true;
        try
        {
            Title = state.Title + (state.Preview ? " — Vista previa ampliada" : " — Reproductor de salida");
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
            _volume.IsEnabled = _mute.IsEnabled = state.ControlVolume;
            _mute.Content = state.Volume <= 0 ? "\uE74F" : "\uE767";
            var muteLabel = state.Volume <= 0 ? "Restaurar volumen" : "Silenciar el programa";
            _mute.ToolTip = muteLabel; AutomationProperties.SetName(_mute, muteLabel);
        }
        finally { _updating = false; }
    }
    public void SetCover(Culto.Core.Cue? cue)
    {
        _cover.Cue = cue; _cover.Visibility = cue is null ? Visibility.Collapsed : Visibility.Visible;
        Monitor.Visibility = cue is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void WireSeek()
    {
        SeekSliderDrag.Attach(_position);
        void Commit() { if (_scrubbing && _state?.CanSeek == true) SeekRequested?.Invoke(_position.Value); _scrubbing = false; }
        _position.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) =>
        { _scrubbing = true; if (e.Handled && !_position.IsMouseCaptureWithin) _position.CaptureMouse(); }), true);
        _position.AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) =>
        { Commit(); if (_position.IsMouseCaptured) _position.ReleaseMouseCapture(); }), true);
        _position.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => _scrubbing = true));
        _position.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, e) =>
        { if (!e.Canceled) Commit(); else _scrubbing = false; }));
        _position.LostMouseCapture += (_, _) => { if (!_position.IsMouseCaptureWithin) _scrubbing = false; };
        _position.PreviewKeyDown += (_, e) => { if (IsSeekKey(e.Key)) _scrubbing = true; };
        _position.PreviewKeyUp += (_, e) => { if (IsSeekKey(e.Key)) Commit(); };
        _position.LostKeyboardFocus += (_, _) => { if (!_position.IsMouseCaptureWithin) _scrubbing = false; };
    }

    private static bool IsSeekKey(Key key) => key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;
    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(double.IsFinite(seconds) ? Math.Max(0, seconds) : 0);
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
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
    public void EnterFullscreen() { if (!_isFullscreen) ToggleFullscreen(); }
    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _restoreState = WindowState;
            _restoreBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            _isFullscreen = true;
            WindowChrome.GetWindowChrome(this).CaptionHeight = 0;
            _caption.Visibility = Visibility.Collapsed;
            var display = DisplayService.GetDisplays().First(d => d.DeviceName == DisplayService.GetWindowDevice(this));
            DisplayService.PlaceWindow(this, display, fullscreen: true);
        }
        else
        {
            _isFullscreen = false;
            WindowChrome.GetWindowChrome(this).CaptionHeight = 34;
            _caption.Visibility = Visibility.Visible;
            WindowState = WindowState.Normal; WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
            Left = _restoreBounds.Left; Top = _restoreBounds.Top; Width = _restoreBounds.Width; Height = _restoreBounds.Height;
            WindowState = _restoreState; Topmost = _pin.IsChecked == true;
        }
        _fullscreen.Content = LineIcon(_isFullscreen ? "M3 8H8V3 M16 3V8H21 M21 16H16V21 M8 21V16H3" : "M8 3H3V8 M16 3H21V8 M21 16V21H16 M3 16V21H8");
        var label = _isFullscreen ? "Salir de pantalla completa · Esc" : "Pantalla completa · F11";
        _fullscreen.ToolTip = label; AutomationProperties.SetName(_fullscreen, label);
    }
}
