using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Culto.Core;
using Culto.Remote;
using QRCoder;

namespace Culto.Desktop.Dialogs;

public sealed class RemoteDialog : Window
{
    public Func<Task<RemoteHost>>? StartRequested { get; set; }
    public Func<Task>? StopRequested { get; set; }
    public Action<List<DeckButton>>? DeckChanged { get; set; }
    public Func<RemoteSnapshot>? SnapshotRequested { get; set; }
    private RemoteHost? _host;
    private bool _changingConnection;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17 };
    private readonly TextBlock _stateLabel = new() { FontSize = 12, FontWeight = FontWeights.SemiBold };
    private readonly TextBox _url = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, MinHeight = 64, MaxHeight = 90, AcceptsReturn = true };
    private readonly Image _qr = new() { Width = 108, Height = 108, Stretch = Stretch.Uniform };
    private readonly TextBlock _qrEmpty = new() { TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = 12, Width = 100, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _qrFrame = new() { Width = 124, Height = 124, CornerRadius = new CornerRadius(14) };
    private readonly ComboBox _addresses = new();
    private readonly Button _start = new() { Content = "Activar mando", Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _stop = new() { Content = "Desactivar" };
    private readonly Button _copy = new() { Content = "Copiar enlace", MinHeight = 30, FontSize = 12, Padding = new Thickness(11, 6, 11, 6), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _addressLabel = new() { FontSize = 12, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _accessLabel = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _connectedCount = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _devices = new();
    private readonly TextBlock _phonePlan = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _phoneLiveLabel = new() { FontSize = 12, FontWeight = FontWeights.Medium };
    private readonly TextBlock _phoneBody = new() { FontSize = 13, FontWeight = FontWeights.Medium, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxHeight = 100, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _phoneReference = new() { FontSize = 12, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly System.Windows.Controls.Primitives.UniformGrid _phoneDeck = new() { Columns = 2 };
    private readonly Grid _pages = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private UIElement? _connectionPage, _deckPage;
    private string? _qrUrl;
    private readonly ObservableCollection<DeckButton> _buttons;
    private readonly ListBox _list = new() { Height = 340 };
    private readonly TextBlock _deckCount = new() { FontSize = 12 };
    private readonly TextBlock _editorHeading = new() { FontSize = 18, FontWeight = FontWeights.SemiBold };
    private readonly Button _remove = new() { Content = "Quitar" };
    private readonly TextBox _label = new();
    private readonly ComboBox _action = new(), _cue = new(), _color = new();
    private readonly string[] _actionValues = ["project", "next", "previous", "blackout", "background", "pause", "restart", "cue"];
    private static readonly string[] ActionLabels = ["Proyectar preparado", "Siguiente", "Anterior", "Pantalla negra / restaurar", "Logo", "Pausar / continuar", "Reiniciar video", "Proyectar un contenido"];
    private readonly string[] _colors = ["#43D9BE", "#A7C7FF", "#FFCF75", "#FF9DAD"];

    public RemoteDialog(RemoteHost? host, IReadOnlyList<DeckButton> buttons, IReadOnlyList<Cue> cues)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        _host = host; _buttons = new(buttons);
        Title = "Control remoto — Culto Studio";
        Width = Math.Min(822, SystemParameters.WorkArea.Width - 32);
        Height = Math.Min(722, SystemParameters.WorkArea.Height - 32);
        MinHeight = 500; MinWidth = 640;
        MaxHeight = Math.Max(500, SystemParameters.WorkArea.Height - 16);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.SingleBorderWindow; Background = ColorBrush("#242424");
        ResizeMode = ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false
        });
        var shell = new Border
        {
            Background = ColorBrush("#242424"),
            Padding = new Thickness(28), BorderBrush = ColorBrush("#353535"), BorderThickness = new Thickness(1),
        };
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition());
        var header = new Grid { Margin = new Thickness(0, 0, 0, 20), Background = Brushes.Transparent };
        header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = "Control remoto", FontSize = 22, FontWeight = FontWeights.Bold });
        heading.Children.Add(Muted("Controla la proyección desde tu teléfono.", new Thickness(0, 7, 0, 0)));
        heading.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 1) DragMove(); };
        header.Children.Add(heading);
        var close = new Button
        {
            Content = Glyph("M6 6 L18 18 M18 6 L6 18", 16), IsCancel = true, Width = 32, Height = 32, MinHeight = 32,
            Padding = new Thickness(8), VerticalAlignment = VerticalAlignment.Top, Style = (Style)FindResource("QuietButton"),
            ToolTip = "Cerrar"
        };
        AutomationProperties.SetName(close, "Cerrar control remoto");
        close.Click += (_, _) => Close(); Grid.SetColumn(close, 1); header.Children.Add(close); root.Children.Add(header);
        _connectionPage = BuildConnection();
        _deckPage = BuildDeckPage(cues); _deckPage.Visibility = Visibility.Collapsed;
        _pages.Children.Add(_connectionPage); _pages.Children.Add(_deckPage); Grid.SetRow(_pages, 1); root.Children.Add(_pages);
        shell.Child = root; Content = shell;
        _start.Click += async (_, _) => await Start();
        _stop.Click += async (_, _) => await Stop();
        _copy.Click += (_, _) => { if (_host is not null && _url.Text.Length > 0) Clipboard.SetText(_url.Text); };
        if (_buttons.Count > 0) _list.SelectedIndex = 0; else SelectButton();
        RefreshConnection();
        _refreshTimer.Tick += (_, _) => { RefreshDevices(); RefreshPreview(); };
        Loaded += (_, _) => { RefreshPreview(); _refreshTimer.Start(); };
        Closed += (_, _) => _refreshTimer.Stop();
    }

    private UIElement BuildConnection()
    {
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290, GridUnitType.Star) });
        var connection = new StackPanel();
        connection.Children.Add(Section("Conecta tu dispositivo"));
        connection.Children.Add(Muted("Escanea el código QR en la misma red Wi-Fi.", new Thickness(0, 6, 0, 16)));
        var pairing = new Grid();
        pairing.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pairing.ColumnDefinitions.Add(new ColumnDefinition());
        var qrLayers = new Grid(); qrLayers.Children.Add(_qr); qrLayers.Children.Add(_qrEmpty); _qrFrame.Child = qrLayers;
        RenderOptions.SetBitmapScalingMode(_qr, BitmapScalingMode.NearestNeighbor);
        AutomationProperties.SetName(_qr, "Código QR para vincular el teléfono");
        pairing.Children.Add(_qrFrame);
        var link = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var addressHeading = Muted("Dirección local", new Thickness(0, 0, 0, 5)); addressHeading.FontSize = 12; link.Children.Add(addressHeading); link.Children.Add(_addressLabel);
        var protection = new Grid { Margin = new Thickness(0, 8, 0, 11) };
        protection.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); protection.ColumnDefinitions.Add(new ColumnDefinition());
        var networkIcon = Glyph("M3 3 H21 V15 H3 Z M12 15 V21 M7 21 H17", 12);
        networkIcon.Margin = new Thickness(0, 1, 5, 0); protection.Children.Add(networkIcon);
        Grid.SetColumn(_accessLabel, 1); protection.Children.Add(_accessLabel); link.Children.Add(protection); link.Children.Add(_copy);
        Grid.SetColumn(link, 1); pairing.Children.Add(link);
        connection.Children.Add(new Border { Background = ColorBrush("#303030"), CornerRadius = new CornerRadius(24), Padding = new Thickness(16), Child = pairing });
        var state = new StackPanel { Margin = new Thickness(2, 14, 0, 0) }; state.Children.Add(_stateLabel); _status.Margin = new Thickness(0, 5, 0, 0); _status.Foreground = (Brush)FindResource("MutedBrush"); state.Children.Add(_status); connection.Children.Add(state);
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        var deviceHeading = new DockPanel { Margin = new Thickness(0, 24, 0, 12) };
        _connectedCount.Foreground = (Brush)FindResource("MutedBrush"); DockPanel.SetDock(_connectedCount, Dock.Right); deviceHeading.Children.Add(_connectedCount); deviceHeading.Children.Add(Section("Dispositivos")); connection.Children.Add(deviceHeading);
        connection.Children.Add(new ScrollViewer { Content = _devices, MaxHeight = 142, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var controls = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) };
        _start.Style = (Style)FindResource("PrimaryButton"); _start.Foreground = ColorBrush("#10271E");
        controls.Children.Add(_start); controls.Children.Add(_stop); connection.Children.Add(controls);
        var options = new StackPanel();
        DialogUi.Label(options, "Dirección de esta computadora", _addresses); options.Children.Add(_addresses);
        RefreshAddresses(); _addresses.SelectionChanged += (_, _) => RefreshConnection();
        var refresh = new Button { Content = "Actualizar red", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), FontSize = 12, MinHeight = 32 };
        refresh.Click += (_, _) => { RefreshAddresses(); RefreshConnection(); }; options.Children.Add(refresh);
        DialogUi.Label(options, "Dirección del mando", _url); options.Children.Add(_url);
        options.Children.Add(Muted("Los dispositivos de esta red pueden controlar el programa desde esta dirección mientras el mando esté activado.", new Thickness(0, 8, 0, 0)));
        connection.Children.Add(new Expander { Header = "Opciones de conexión", Foreground = (Brush)FindResource("TextBrush"), Content = options, Margin = new Thickness(0, 16, 0, 0), FontSize = 12 });
        var edit = new Button { Content = "Editar botones", HorizontalAlignment = HorizontalAlignment.Left, Style = (Style)FindResource("QuietButton"), Margin = new Thickness(-10, 12, 0, 0), FontSize = 12 };
        edit.Click += (_, _) => ShowDeck(true); connection.Children.Add(edit);
        var hint = Muted("Cerrar esta ventana mantiene el mando activo.", new Thickness(0, 7, 0, 2)); hint.FontSize = 12; connection.Children.Add(hint);
        columns.Children.Add(Scroll(connection));
        var preview = BuildPhonePanel(); Grid.SetColumn(preview, 2); columns.Children.Add(preview);
        return columns;
    }

    private UIElement BuildDeckPage(IReadOnlyList<Cue> cues)
    {
        var page = new DockPanel();
        var back = new Button
        {
            Content = "‹  Volver a la conexión", HorizontalAlignment = HorizontalAlignment.Left,
            Style = (Style)FindResource("QuietButton"), Margin = new Thickness(-10, 0, 0, 12)
        };
        back.Click += (_, _) => ShowDeck(false); DockPanel.SetDock(back, Dock.Top); page.Children.Add(back);
        page.Children.Add(Scroll(BuildDeck(cues))); return page;
    }

    private void ShowDeck(bool show)
    {
        if (_connectionPage is null || _deckPage is null) return;
        _connectionPage.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        _deckPage.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show) _list.Focus(); else RefreshPreview();
    }

    private UIElement BuildPhonePanel()
    {
        var panel = new Border { Background = StripeBrush(), CornerRadius = new CornerRadius(24), Padding = new Thickness(10, 14, 10, 14) };
        var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var phone = new Border
        {
            MaxWidth = 264, MinHeight = 540, Background = ColorBrush("#101113"), CornerRadius = new CornerRadius(48),
            Padding = new Thickness(8), BorderBrush = ColorBrush("#515258"), BorderThickness = new Thickness(2),
            IsHitTestVisible = false
        };
        AutomationProperties.SetName(phone, "Vista previa del mando con el estado actual de la proyección");
        var screen = new Border { Background = ColorBrush("#191919"), CornerRadius = new CornerRadius(38), Padding = new Thickness(14, 28, 14, 14) };
        var phoneLayers = new Grid(); phoneLayers.Children.Add(screen);
        phoneLayers.Children.Add(new Border { Width = 66, Height = 13, CornerRadius = new CornerRadius(7), Background = ColorBrush("#101113"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 7, 0, 0) });
        phone.Child = phoneLayers;
        var content = new StackPanel();
        var brand = new Grid { Margin = new Thickness(0, 0, 0, 12) }; brand.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); brand.ColumnDefinitions.Add(new ColumnDefinition());
        brand.Children.Add(new Border { Width = 27, Height = 27, CornerRadius = new CornerRadius(9), Background = (Brush)FindResource("ActionBrush"), Child = Glyph("M12 5 L12 19 M6 10 L18 10", 18, (Brush)FindResource("OnActionBrush")) });
        var brandText = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        brandText.Children.Add(new TextBlock { Text = "Culto Deck", FontSize = 12, FontWeight = FontWeights.SemiBold });
        _phonePlan.Foreground = (Brush)FindResource("MutedBrush"); _phonePlan.Margin = new Thickness(0, 2, 0, 0); brandText.Children.Add(_phonePlan);
        Grid.SetColumn(brandText, 1); brand.Children.Add(brandText); content.Children.Add(brand);
        _phoneLiveLabel.Foreground = ColorBrush("#38D399"); _phoneLiveLabel.Margin = new Thickness(2, 0, 0, 7); content.Children.Add(_phoneLiveLabel);
        var live = new Grid { Margin = new Thickness(14, 10, 14, 10) };
        live.RowDefinitions.Add(new RowDefinition()); live.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _phoneBody.Foreground = Brushes.White; _phoneBody.VerticalAlignment = VerticalAlignment.Center;
        _phoneReference.Foreground = ColorBrush("#D4D5DA"); live.Children.Add(_phoneBody); Grid.SetRow(_phoneReference, 1); live.Children.Add(_phoneReference);
        content.Children.Add(new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(18), Height = 112, ClipToBounds = true, Child = live });
        var navigation = new Grid { Margin = new Thickness(0, 8, 0, 12) }; navigation.ColumnDefinitions.Add(new ColumnDefinition()); navigation.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) }); navigation.ColumnDefinitions.Add(new ColumnDefinition());
        navigation.Children.Add(PhonePill("Anterior", "M14 6 L8 12 L14 18")); var next = PhonePill("Siguiente", "M10 6 L16 12 L10 18", true); Grid.SetColumn(next, 2); navigation.Children.Add(next); content.Children.Add(navigation);
        content.Children.Add(new TextBlock { Text = "Accesos rápidos", FontSize = 12, FontWeight = FontWeights.Medium, Margin = new Thickness(2, 0, 0, 6) });
        content.Children.Add(_phoneDeck); screen.Child = content;
        layout.Children.Add(new ScrollViewer
        {
            Content = phone, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        });
        var caption = Muted("Vista previa del mando", new Thickness(0, 12, 0, 0)); caption.FontSize = 12; caption.TextAlignment = TextAlignment.Center;
        Grid.SetRow(caption, 1); layout.Children.Add(caption); panel.Child = layout; return panel;
    }

    private static Border PhonePill(string label, string geometry, bool primary = false)
    {
        var ink = primary ? ColorBrush("#171717") : ColorBrush("#D7D8DE");
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.Add(Glyph(geometry, 12, ink)); row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = ink, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return new Border { CornerRadius = new CornerRadius(18), Height = 36, Background = primary ? Brushes.White : ColorBrush("#303030"), Child = row };
    }

    private static System.Windows.Shapes.Path Glyph(string geometry, double size, Brush? brush = null) => new()
    {
        Data = Geometry.Parse(geometry), Width = size, Height = size, Stretch = Stretch.Uniform,
        Stroke = brush ?? ColorBrush("#D7D8DE"), StrokeThickness = 1.7, StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, VerticalAlignment = VerticalAlignment.Center
    };

    private static SolidColorBrush ColorBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush;
    }

    private static DrawingBrush StripeBrush()
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(ColorBrush("#27282C"), null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        var pen = new Pen(ColorBrush("#303135"), .7); pen.Freeze();
        drawing.Children.Add(new GeometryDrawing(null, pen, Geometry.Parse("M-8 8 L8 -8 M0 16 L16 0 M8 24 L24 8")));
        var brush = new DrawingBrush(drawing) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 16, 16), ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 16, 16) };
        brush.Freeze(); return brush;
    }

    private void RefreshDevices()
    {
        _devices.Children.Clear();
        var clients = _host?.Clients;
        var connected = _host?.ConnectedClientCount ?? 0;
        _connectedCount.Text = connected == 1 ? "1 conectado" : $"{connected} conectados";
        if (clients is null || clients.Count == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(13, 11, 13, 11) };
            empty.Children.Add(new TextBlock { Text = "Aún no hay dispositivos", FontSize = 12, FontWeight = FontWeights.Medium });
            var hint = Muted(_host is null ? "Activa el mando para conectar tu teléfono." : "Escanea el QR para empezar.", new Thickness(0, 4, 0, 0)); hint.FontSize = 12; empty.Children.Add(hint);
            _devices.Children.Add(new Border { CornerRadius = new CornerRadius(20), Background = ColorBrush("#303030"), Child = empty }); return;
        }
        foreach (var client in clients.OrderByDescending(client => client.IsConnected).ThenByDescending(client => client.LastSeen))
        {
            var row = new Grid { Margin = new Thickness(14, 10, 14, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = string.IsNullOrWhiteSpace(client.Name) ? "Dispositivo" : client.Name;
            var isPhone = name.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || name.Contains("Android", StringComparison.OrdinalIgnoreCase) || name.Contains("iPad", StringComparison.OrdinalIgnoreCase) || name.Contains("móvil", StringComparison.OrdinalIgnoreCase) || name.Contains("teléfono", StringComparison.OrdinalIgnoreCase);
            row.Children.Add(Glyph(isPhone ? "M7 2 L17 2 Q19 2 19 4 L19 20 Q19 22 17 22 L7 22 Q5 22 5 20 L5 4 Q5 2 7 2 Z M10 18 L14 18" : "M3 4 L21 4 L21 17 L3 17 Z M12 17 L12 21 M7 21 L17 21", 20));
            var labels = new StackPanel { Margin = new Thickness(9, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            labels.Children.Add(new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = name });
            labels.Children.Add(new TextBlock { Text = client.IsConnected ? "Conectado" : LastSeen(client.LastSeen), FontSize = 12, Margin = new Thickness(0, 3, 0, 0), Foreground = client.IsConnected ? ColorBrush("#38D399") : (Brush)FindResource("MutedBrush") });
            Grid.SetColumn(labels, 1); row.Children.Add(labels);
            var dot = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Fill = client.IsConnected ? ColorBrush("#24B27E") : ColorBrush("#777A84"), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(dot, 2); row.Children.Add(dot);
            _devices.Children.Add(new Border { Background = ColorBrush("#303030"), CornerRadius = new CornerRadius(24), Margin = new Thickness(0, 0, 0, 8), Child = row });
        }
    }

    private static string LastSeen(DateTimeOffset seen)
    {
        var elapsed = DateTimeOffset.UtcNow - seen;
        if (elapsed.TotalMinutes < 1) return "Visto hace un momento";
        if (elapsed.TotalHours < 1) return $"Visto hace {(int)elapsed.TotalMinutes} min";
        return "Última vez " + seen.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    private void RefreshPreview()
    {
        var snapshot = SnapshotRequested?.Invoke();
        _phonePlan.Text = snapshot?.PlanName ?? "Control remoto";
        _phoneLiveLabel.Text = snapshot is { HasOutput: true } ? (snapshot.IsBlackout ? "PANTALLA NEGRA" : snapshot.IsBackground ? "LOGO EN PANTALLA" : "EN PANTALLA") : "SALIDA CERRADA";
        _phoneBody.Text = snapshot switch
        {
            null => "Sin vista previa",
            { HasOutput: false } => "Salida cerrada",
            { IsBlackout: true } => "",
            { IsBackground: true } => "Logo",
            { LiveBody: { Length: > 0 } body } => body,
            _ => snapshot.LiveTitle
        };
        _phoneReference.Text = snapshot is { HasOutput: true, IsBlackout: false, IsBackground: false }
            ? snapshot.LiveReference ?? (string.IsNullOrWhiteSpace(snapshot.LiveBody) ? "" : snapshot.LiveTitle) : "";
        _phoneReference.Visibility = _phoneReference.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        _phoneDeck.Children.Clear();
        foreach (var button in _buttons.Take(6))
        {
            var mappedColor = button.Color.ToUpperInvariant() switch
            {
                "#43D9BE" => "#BEF264", "#A7C7FF" => "#4274F4", "#FFCF75" => "#F8BA2A", "#FF9DAD" => "#EF5350", _ => button.Color
            };
            SolidColorBrush color;
            try { color = ColorBrush(mappedColor); } catch (FormatException) { color = (SolidColorBrush)FindResource("ActionBrush"); }
            var ink = PreviewInk(color.Color);
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) };
            labels.Children.Add(Glyph(ActionIcon(button.Action), 21, ink));
            labels.Children.Add(new TextBlock { Text = button.Label, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = ink, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = 18, MaxHeight = 36, Margin = new Thickness(0, 9, 0, 0) });
            _phoneDeck.Children.Add(new Border { Background = color, CornerRadius = new CornerRadius(24), Height = 92, Margin = new Thickness(2, 2, 2, 2), Child = labels, ToolTip = button.Label });
        }
        if (_buttons.Count == 0)
            _phoneDeck.Children.Add(new TextBlock { Text = "Sin accesos", FontSize = 12, Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) });
    }

    private static Brush PreviewInk(Color color)
    {
        static double Linear(byte component)
        {
            var value = component / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
        var luminance = .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        return luminance > .179 ? ColorBrush("#101114") : Brushes.White;
    }

    private static string ActionIcon(string action) => action switch
    {
        "next" => "M8 5 L16 12 L8 19 M20 5 L20 19",
        "previous" => "M16 5 L8 12 L16 19 M4 5 L4 19",
        "blackout" => "M3 4 L21 4 L21 17 L3 17 Z M12 17 L12 21 M7 21 L17 21 M4 20 L20 2",
        "background" => "M3 4 L21 4 L21 20 L3 20 Z M3 16 L8 11 L13 15 L17 9 L21 13 M7 8 L8 8",
        "pause" => "M8 5 L8 19 M16 5 L16 19",
        "restart" => "M5 8 A8 8 0 1 1 4 15 M5 3 L5 9 L11 9",
        "cue" => "M5 3 L19 3 L19 21 L5 21 Z M9 7 L15 7 M9 12 L15 12 M9 17 L13 17",
        _ => "M3 4 L21 4 L21 17 L3 17 Z M10 7 L15 10.5 L10 14 Z M7 21 L17 21 M12 17 L12 21"
    };

    private UIElement BuildDeck(IReadOnlyList<Cue> cues)
    {
        var columns = new Grid { Margin = new Thickness(0, 4, 8, 0) };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var preview = new StackPanel();
        preview.Children.Add(Section("Botones del teléfono")); _deckCount.Margin = new Thickness(0, 5, 0, 12); _deckCount.Foreground = (Brush)FindResource("MutedBrush"); preview.Children.Add(_deckCount);
        _list.ItemsSource = _buttons; _list.ItemTemplate = DeckTemplate(); _list.SelectionChanged += (_, _) => SelectButton();
        AutomationProperties.SetName(_list, "Botones del mando: selecciona uno para editar");
        preview.Children.Add(_list);
        preview.Children.Add(Muted("Selecciona un acceso para editarlo. El color también se verá en el teléfono.", new Thickness(0, 12, 0, 0)));
        var previewCard = DialogUi.Card(preview); previewCard.Margin = new Thickness(0, 0, 14, 0); previewCard.Padding = new Thickness(14); columns.Children.Add(previewCard);
        var editor = new StackPanel();
        editor.Children.Add(_editorHeading); editor.Children.Add(Muted("Los cambios se guardan al pulsar Guardar botón.", new Thickness(0, 5, 0, 8)));
        DialogUi.Label(editor, "Nombre del botón", _label); editor.Children.Add(_label);
        DialogUi.Label(editor, "Acción", _action); _action.ItemsSource = ActionLabels; _action.SelectedIndex = 0;
        _action.SelectionChanged += (_, _) => _cue.IsEnabled = _action.SelectedIndex == 7; editor.Children.Add(_action);
        DialogUi.Label(editor, "Contenido del acceso directo", _cue);
        _cue.ItemsSource = cues; _cue.DisplayMemberPath = "Title"; _cue.IsEnabled = false;
        _cue.IsTextSearchEnabled = true; _cue.IsTextSearchCaseSensitive = false; TextSearch.SetTextPath(_cue, "Title"); _cue.MaxDropDownHeight = 280;
        _cue.SetValue(VirtualizingPanel.IsVirtualizingProperty, true); _cue.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
        _cue.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        editor.Children.Add(_cue);
        editor.Children.Add(Muted("Escribe el número o el comienzo del título para encontrarlo.", new Thickness(0, 5, 0, 0)));
        DialogUi.Label(editor, "Color del acceso", _color); _color.ItemsSource = new[] { "Turquesa", "Azul", "Ámbar", "Rosa" }; _color.SelectedIndex = 0; editor.Children.Add(_color);
        var actions = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
        var save = DialogUi.Button("Guardar botón", SaveButton, true); save.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(save);
        var add = DialogUi.Button("Nuevo", () => { _list.SelectedItem = null; _label.Text = ""; _action.SelectedIndex = 0; _color.SelectedIndex = 0; _cue.SelectedItem = null; SelectButton(); }); add.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(add);
        _remove.Margin = new Thickness(0, 0, 0, 8);
        _remove.Click += (_, _) => { if (_list.SelectedItem is DeckButton button) { _buttons.Remove(button); DeckChanged?.Invoke(_buttons.ToList()); UpdateDeckCount(); } };
        actions.Children.Add(_remove); editor.Children.Add(actions);
        var editorCard = DialogUi.Card(editor); Grid.SetColumn(editorCard, 1); columns.Children.Add(editorCard);
        UpdateDeckCount(); return columns;
    }

    private TextBlock Muted(string text, Thickness margin) => new() { Text = text, Style = (Style)FindResource("MutedText"), Margin = margin };
    private static TextBlock Section(string text, Thickness? margin = null) => new() { Text = text, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0) };
    private static ScrollViewer Scroll(UIElement content) => new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private void UpdateDeckCount()
    {
        _deckCount.Text = $"{_buttons.Count} de 32 accesos disponibles";
        RefreshPreview();
    }
    private static DataTemplate DeckTemplate()
    {
        var row = new FrameworkElementFactory(typeof(DockPanel));
        row.SetValue(FrameworkElement.MinHeightProperty, 46.0);
        var stripe = new FrameworkElementFactory(typeof(Border));
        stripe.SetValue(FrameworkElement.WidthProperty, 4.0); stripe.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        stripe.SetValue(Border.CornerRadiusProperty, new CornerRadius(2)); stripe.SetBinding(Border.BackgroundProperty, new Binding("Color"));
        stripe.SetValue(DockPanel.DockProperty, Dock.Left); row.AppendChild(stripe);
        var labels = new FrameworkElementFactory(typeof(StackPanel)); labels.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var label = new FrameworkElementFactory(typeof(TextBlock)); label.SetBinding(TextBlock.TextProperty, new Binding("Label")); label.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); label.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); labels.AppendChild(label);
        var action = new FrameworkElementFactory(typeof(TextBlock)); action.SetBinding(TextBlock.TextProperty, new Binding("Action") { Converter = new ActionLabelConverter() }); action.SetValue(TextBlock.FontSizeProperty, 12d); action.SetValue(TextBlock.StyleProperty, Application.Current.FindResource("ItemMetadataText")); action.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 0)); action.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); labels.AppendChild(action);
        row.AppendChild(labels); return new DataTemplate { VisualTree = row };
    }
    private sealed class ActionLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value as string) switch
        {
            "project" => "Proyectar preparado", "next" => "Siguiente", "previous" => "Anterior", "blackout" => "Pantalla negra / restaurar",
            "background" => "Logo", "pause" => "Pausar / continuar", "restart" => "Reiniciar video", "cue" => "Contenido del culto", _ => "Acción del culto"
        };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private async Task Start()
    {
        if (StartRequested is null || _changingConnection || _host is not null) return;
        RefreshAddresses();
        _changingConnection = true;
        UpdateConnectionControls();
        _start.IsEnabled = false; _status.Text = "Activando mando…"; _stateLabel.Text = "ACTIVANDO";
        try { _host = await StartRequested(); _changingConnection = false; RefreshConnection(); }
        catch (Exception ex) { _status.Text = "No se pudo activar el mando: " + ex.Message; _stateLabel.Text = "NO SE PUDO ACTIVAR"; _stateLabel.Foreground = (Brush)FindResource("AmberBrush"); _start.IsEnabled = true; }
        finally { _changingConnection = false; UpdateConnectionControls(); }
    }
    private async Task Stop()
    {
        if (_changingConnection || _host is null) return;
        _changingConnection = true;
        UpdateConnectionControls();
        _stop.IsEnabled = false; _copy.IsEnabled = false; _stateLabel.Text = "DESACTIVANDO";
        try { if (StopRequested is not null) await StopRequested(); _host = null; _changingConnection = false; RefreshConnection(); }
        catch (Exception ex) { _status.Text = "No se pudo cerrar el mando: " + ex.Message; _stateLabel.Text = "REVISA LA CONEXIÓN"; _stateLabel.Foreground = (Brush)FindResource("AmberBrush"); _stop.IsEnabled = true; _copy.IsEnabled = _host is not null; }
        finally { _changingConnection = false; UpdateConnectionControls(); }
    }
    private void UpdateConnectionControls()
    {
        _start.IsEnabled = !_changingConnection && _host is null;
        _stop.IsEnabled = !_changingConnection && _host is not null;
        _copy.IsEnabled = !_changingConnection && _host is not null && _addresses.SelectedItem is string;
        _addresses.IsEnabled = !_changingConnection && _host is not null;
    }
    private void RefreshConnection()
    {
        UpdateConnectionControls();
        if (_changingConnection) return;
        var active = _host is not null;
        var address = _addresses.SelectedItem as string;
        var canConnect = active && address is not null;
        _stateLabel.Text = active ? "MANDO ACTIVO" : "MANDO DESACTIVADO";
        _stateLabel.Foreground = (Brush)FindResource(active ? "LiveTextBrush" : "MutedBrush");
        _status.Text = active ? (canConnect ? "Listo para vincular. Escanea el QR desde tu teléfono." : "Sin conexión de red. Conecta la PC y pulsa Actualizar red.") : "Actívalo cuando quieras conectar el teléfono.";
        _qrFrame.Background = Brushes.White;
        _qrFrame.BorderThickness = new Thickness(0);
        _qr.Visibility = canConnect ? Visibility.Visible : Visibility.Collapsed; _qrEmpty.Visibility = canConnect ? Visibility.Collapsed : Visibility.Visible;
        _qrEmpty.Text = active ? "Sin dirección de red" : "El QR aparecerá aquí\nal activar el mando.";
        _qrEmpty.Foreground = ColorBrush("#555860");
        _addressLabel.Text = canConnect ? $"{address}:{_host!.Port}" : active ? "Red no disponible" : "Mando desactivado";
        _accessLabel.Text = active ? "Acceso por red local" : "Enlace al activar";
        _accessLabel.Foreground = (Brush)FindResource(active ? "LiveTextBrush" : "MutedBrush");
        RefreshDevices(); RefreshPreview();
        if (_host is null) { _url.Text = "Activa el mando para generar el enlace de vinculación."; _qr.Source = null; _qrUrl = null; return; }
        if (address is null) { _url.Text = "Sin dirección de red disponible."; _qr.Source = null; _qrUrl = null; return; }
        _url.Text = $"http://{address}:{_host.Port}/";
        if (_qrUrl == _url.Text && _qr.Source is not null) return;
        using var data = QRCodeGenerator.GenerateQrCode(_url.Text, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        using var stream = new MemoryStream(qr.GetGraphic(8));
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        _qr.Source = bitmap; _qrUrl = _url.Text;
    }
    private void RefreshAddresses()
    {
        var selected = _addresses.SelectedItem as string;
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .Select(n => n.GetIPProperties())
            .OrderByDescending(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                && !g.Address.Equals(IPAddress.Any)))
            .SelectMany(p => p.UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)
                && !a.Address.Equals(IPAddress.Any) && !(a.Address.GetAddressBytes()[0] == 169 && a.Address.GetAddressBytes()[1] == 254))
            .Select(a => a.Address.ToString()).Distinct().ToList();
        _addresses.ItemsSource = addresses;
        _addresses.SelectedItem = selected is not null && addresses.Contains(selected) ? selected : addresses.FirstOrDefault();
    }
    private void SelectButton()
    {
        _remove.IsEnabled = _list.SelectedItem is DeckButton;
        _editorHeading.Text = _list.SelectedItem is DeckButton ? "Editar botón" : "Nuevo botón";
        if (_list.SelectedItem is not DeckButton button) return;
        _label.Text = button.Label; _action.SelectedIndex = Array.IndexOf(_actionValues, button.Action);
        _color.SelectedIndex = Math.Max(0, Array.IndexOf(_colors, button.Color));
        _cue.SelectedItem = (_cue.ItemsSource as IReadOnlyList<Cue>)?.FirstOrDefault(c => c.Id.ToString() == button.CueId);
    }
    private void SaveButton()
    {
        if (string.IsNullOrWhiteSpace(_label.Text) || _action.SelectedIndex < 0) { MessageBox.Show(this, "Escribe un nombre y elige una acción."); return; }
        if (_action.SelectedIndex == 7 && _cue.SelectedItem is not Cue) { MessageBox.Show(this, "Selecciona el contenido de este botón."); return; }
        var old = _list.SelectedItem as DeckButton;
        if (old is null && _buttons.Count >= 32) { MessageBox.Show(this, "Puedes guardar hasta 32 botones."); return; }
        var button = new DeckButton(old?.Id ?? Guid.NewGuid().ToString("N"), _label.Text.Trim(), _colors[Math.Max(0, _color.SelectedIndex)], _actionValues[_action.SelectedIndex], (_cue.SelectedItem as Cue)?.Id.ToString());
        if (old is not null) _buttons[_buttons.IndexOf(old)] = button; else _buttons.Add(button);
        _list.SelectedItem = button; DeckChanged?.Invoke(_buttons.ToList()); UpdateDeckCount();
    }
}
