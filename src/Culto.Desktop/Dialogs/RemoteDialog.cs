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
using Culto.Core;
using Culto.Remote;
using QRCoder;

namespace Culto.Desktop.Dialogs;

public sealed class RemoteDialog : Window
{
    public Func<Task<RemoteHost>>? StartRequested { get; set; }
    public Func<Task>? StopRequested { get; set; }
    public Action<List<DeckButton>>? DeckChanged { get; set; }
    private RemoteHost? _host;
    private bool _changingConnection;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _stateLabel = new() { FontSize = 11, FontWeight = FontWeights.Bold };
    private readonly TextBox _url = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, MinHeight = 50, MaxHeight = 90 };
    private readonly Image _qr = new() { Width = 176, Height = 176, Margin = new Thickness(12), Stretch = Stretch.Uniform };
    private readonly TextBlock _qrEmpty = new() { Text = "El QR aparecerá aquí\nal activar el mando.", TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Width = 160, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _qrFrame = new() { Width = 200, Height = 200, CornerRadius = new CornerRadius(8) };
    private readonly ComboBox _addresses = new();
    private readonly Button _start = new() { Content = "Activar mando", Margin = new Thickness(0, 0, 8, 8) };
    private readonly Button _stop = new() { Content = "Desactivar", Margin = new Thickness(0, 0, 0, 8) };
    private readonly Button _copy = new() { Content = "Copiar enlace", MinHeight = 36 };
    private readonly ObservableCollection<DeckButton> _buttons;
    private readonly ListBox _list = new() { Height = 340 };
    private readonly TextBlock _deckCount = new() { FontSize = 12 };
    private readonly TextBlock _editorHeading = new() { FontSize = 18, FontWeight = FontWeights.SemiBold };
    private readonly Button _remove = new() { Content = "Quitar" };
    private readonly TextBox _label = new();
    private readonly ComboBox _action = new(), _cue = new(), _color = new();
    private readonly string[] _actionValues = ["project", "next", "previous", "blackout", "background", "pause", "restart", "cue"];
    private static readonly string[] ActionLabels = ["Proyectar preparado", "Siguiente", "Anterior", "Pantalla negra / restaurar", "Mostrar fondo", "Pausar / continuar", "Reiniciar video", "Proyectar un contenido"];
    private readonly string[] _colors = ["#43D9BE", "#A7C7FF", "#FFCF75", "#FF9DAD"];

    public RemoteDialog(RemoteHost? host, IReadOnlyList<DeckButton> buttons, IReadOnlyList<Cue> cues)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        _host = host; _buttons = new(buttons);
        Title = "Mando del teléfono — Culto Studio";
        Width = Math.Min(840, SystemParameters.WorkArea.Width - 48);
        Height = Math.Min(700, SystemParameters.WorkArea.Height - 48);
        MinHeight = 500; MinWidth = 640;
        MaxHeight = Math.Max(500, SystemParameters.WorkArea.Height - 32);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(24) };
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock { Text = "El culto, a un toque", FontSize = 25, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(Muted("Vincula tu teléfono y organiza sus accesos desde aquí.", new Thickness(0, 5, 0, 0)));
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        var close = new Button { Content = "Listo", IsCancel = true, MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); footer.Children.Add(close);
        var footerHint = Muted("Cerrar esta ventana mantiene el estado del mando.", new Thickness(0, 0, 16, 0));
        footerHint.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(footerHint);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Conectar teléfono", Content = Scroll(BuildConnection()) });
        tabs.Items.Add(new TabItem { Header = "Organizar botones", Content = Scroll(BuildDeck(cues)) });
        root.Children.Add(tabs); Content = root;
        _start.Click += async (_, _) => await Start();
        _stop.Click += async (_, _) => await Stop();
        _copy.Click += (_, _) => { if (_host is not null && _url.Text.Length > 0) Clipboard.SetText(_url.Text); };
        if (_buttons.Count > 0) _list.SelectedIndex = 0; else SelectButton();
        RefreshConnection();
    }

    private UIElement BuildConnection()
    {
        var connection = new StackPanel { Margin = new Thickness(0, 4, 8, 0) };
        var state = new StackPanel();
        state.Children.Add(_stateLabel); _status.Margin = new Thickness(0, 4, 0, 0); state.Children.Add(_status);
        var stateCard = DialogUi.Card(state); stateCard.Padding = new Thickness(14, 10, 14, 10); stateCard.Margin = new Thickness(0, 0, 0, 14); connection.Children.Add(stateCard);
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var steps = new StackPanel();
        steps.Children.Add(Section("1  Activa el mando"));
        steps.Children.Add(Muted("La PC y el teléfono deben estar en la misma red Wi-Fi.", new Thickness(0, 6, 0, 12)));
        var controls = new WrapPanel(); _start.Style = (Style)FindResource("PrimaryButton"); controls.Children.Add(_start); controls.Children.Add(_stop); steps.Children.Add(controls);
        steps.Children.Add(Section("2  Revisa la dirección de red", new Thickness(0, 12, 0, 0)));
        DialogUi.Label(steps, "Dirección de esta computadora", _addresses);
        RefreshAddresses();
        _addresses.SelectionChanged += (_, _) => RefreshConnection(); steps.Children.Add(_addresses);
        var refresh = new Button { Content = "Actualizar red", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        refresh.Click += (_, _) => { RefreshAddresses(); RefreshConnection(); };
        steps.Children.Add(refresh);
        steps.Children.Add(Muted("Si hay varias, elige la conexión que comparte la red del teléfono.", new Thickness(0, 7, 0, 0)));
        var stepsCard = DialogUi.Card(steps); stepsCard.Margin = new Thickness(0, 0, 14, 0); columns.Children.Add(stepsCard);
        var qrContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        qrContent.Children.Add(Section("3  Escanea el QR"));
        qrContent.Children.Add(Muted("Usa la cámara del teléfono.", new Thickness(0, 5, 0, 12)));
        var qrLayers = new Grid(); qrLayers.Children.Add(_qr); qrLayers.Children.Add(_qrEmpty); _qrFrame.Child = qrLayers; qrContent.Children.Add(_qrFrame);
        AutomationProperties.SetName(_qr, "Código QR para vincular el teléfono");
        var qrCard = DialogUi.Card(qrContent); Grid.SetColumn(qrCard, 1); columns.Children.Add(qrCard);
        connection.Children.Add(columns);
        var link = new StackPanel();
        DialogUi.Label(link, "Enlace de vinculación", _url); link.Children.Add(_url);
        var copyRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(_copy, Dock.Right); copyRow.Children.Add(_copy);
        copyRow.Children.Add(Muted("También puedes copiarlo y abrirlo en el navegador del teléfono.", new Thickness(0, 0, 12, 0)));
        link.Children.Add(copyRow);
        var linkCard = DialogUi.Card(link); linkCard.Padding = new Thickness(14, 4, 14, 12); linkCard.Margin = new Thickness(0, 14, 0, 0); connection.Children.Add(linkCard);
        connection.Children.Add(Muted("Mantén Culto Studio abierto. Al desactivar el mando, el enlace deja de funcionar. Usa una red local de confianza.", new Thickness(0, 12, 0, 0)));
        return connection;
    }

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
    private void UpdateDeckCount() => _deckCount.Text = $"{_buttons.Count} de 32 accesos disponibles";
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
        var action = new FrameworkElementFactory(typeof(TextBlock)); action.SetBinding(TextBlock.TextProperty, new Binding("Action") { Converter = new ActionLabelConverter() }); action.SetValue(TextBlock.FontSizeProperty, 11.0); action.SetValue(TextBlock.ForegroundProperty, Application.Current.FindResource("MutedBrush")); action.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 0)); action.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); labels.AppendChild(action);
        row.AppendChild(labels); return new DataTemplate { VisualTree = row };
    }
    private sealed class ActionLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value as string) switch
        {
            "project" => "Proyectar preparado", "next" => "Siguiente", "previous" => "Anterior", "blackout" => "Pantalla negra / restaurar",
            "background" => "Mostrar fondo", "pause" => "Pausar / continuar", "restart" => "Reiniciar video", "cue" => "Contenido del culto", _ => "Acción del culto"
        };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private async Task Start()
    {
        if (StartRequested is null || _changingConnection || _host is not null) return;
        RefreshAddresses();
        _changingConnection = true;
        _start.IsEnabled = false; _status.Text = "Activando mando…"; _stateLabel.Text = "ACTIVANDO";
        try { _host = await StartRequested(); _changingConnection = false; RefreshConnection(); }
        catch (Exception ex) { _status.Text = "No se pudo activar el mando: " + ex.Message; _stateLabel.Text = "NO SE PUDO ACTIVAR"; _stateLabel.Foreground = (Brush)FindResource("AmberBrush"); _start.IsEnabled = true; }
        finally { _changingConnection = false; UpdateConnectionControls(); }
    }
    private async Task Stop()
    {
        if (_changingConnection || _host is null) return;
        _changingConnection = true;
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
        _stateLabel.Foreground = (Brush)FindResource(active ? "AccentBrush" : "MutedBrush");
        _status.Text = active ? (canConnect ? "Listo para vincular. Escanea el QR desde tu teléfono." : "Sin conexión de red. Conecta la PC y pulsa Actualizar red.") : "Actívalo cuando quieras conectar el teléfono.";
        _qrFrame.Background = canConnect ? Brushes.White : (Brush)FindResource("RaisedBrush");
        _qrFrame.BorderBrush = (Brush)FindResource("BorderBrush"); _qrFrame.BorderThickness = canConnect ? new Thickness(0) : new Thickness(1);
        _qr.Visibility = canConnect ? Visibility.Visible : Visibility.Collapsed; _qrEmpty.Visibility = canConnect ? Visibility.Collapsed : Visibility.Visible;
        _qrEmpty.Text = active ? "Sin dirección de red" : "El QR aparecerá aquí\nal activar el mando.";
        _qrEmpty.Foreground = (Brush)FindResource("MutedBrush");
        if (_host is null) { _url.Text = "Activa el mando para generar el enlace de vinculación."; _qr.Source = null; return; }
        if (address is null) { _url.Text = "Sin dirección de red disponible."; _qr.Source = null; return; }
        _url.Text = $"http://{address}:{_host.Port}/#token={_host.Token}";
        using var data = QRCodeGenerator.GenerateQrCode(_url.Text, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        using var stream = new MemoryStream(qr.GetGraphic(8));
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        _qr.Source = bitmap;
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
