using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
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
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _url = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
    private readonly Image _qr = new() { Width = 190, Height = 190, Margin = new Thickness(14), Stretch = Stretch.Uniform };
    private readonly ComboBox _addresses = new();
    private readonly Button _start = new() { Content = "Activar mando local" };
    private readonly Button _stop = new() { Content = "Desactivar mando", Margin = new Thickness(8, 0, 0, 0) };
    private readonly ObservableCollection<DeckButton> _buttons;
    private readonly ListBox _list = new() { DisplayMemberPath = "Label", Height = 170 };
    private readonly TextBox _label = new();
    private readonly ComboBox _action = new(), _cue = new(), _color = new();
    private readonly string[] _actionValues = ["project", "next", "previous", "blackout", "background", "pause", "restart", "cue"];
    private readonly string[] _colors = ["#43D9BE", "#A7C7FF", "#FFCF75", "#FF9DAD"];

    public RemoteDialog(RemoteHost? host, IReadOnlyList<DeckButton> buttons, IReadOnlyList<Cue> cues)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        _host = host; _buttons = new(buttons);
        Title = "Mando del teléfono — Culto Studio"; Width = 650; Height = 740;
        MinHeight = 600; MinWidth = 580; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(24) };
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = "Tu consola, en el teléfono", FontSize = 25, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "Botones para acompañar el culto desde la misma red Wi-Fi.", Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 8, 0, 18) });
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var tabs = new TabControl(); root.Children.Add(tabs); Content = root;
        var connection = new StackPanel();
        tabs.Items.Add(new TabItem { Header = "Conectar teléfono", Content = new ScrollViewer { Content = connection, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 16) };
        _start.Style = (Style)FindResource("PrimaryButton"); controls.Children.Add(_start); controls.Children.Add(_stop); connection.Children.Add(controls);
        _start.Click += async (_, _) => await Start(); _stop.Click += async (_, _) => await Stop();
        connection.Children.Add(_status);
        DialogUi.Label(connection, "Dirección de la computadora en la red");
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
            .Select(a => a.Address.ToString()).Distinct().ToList();
        if (addresses.Count == 0) addresses.Add("127.0.0.1");
        _addresses.ItemsSource = addresses; _addresses.SelectedIndex = 0;
        _addresses.SelectionChanged += (_, _) => RefreshConnection(); connection.Children.Add(_addresses);
        var qrPanel = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 18, 0, 12), Child = _qr };
        connection.Children.Add(qrPanel); connection.Children.Add(_url);
        connection.Children.Add(DialogUi.Button("Copiar enlace de vinculación", () => { if (_host is not null && _url.Text.Length > 0) Clipboard.SetText(_url.Text); }));
        connection.Children.Add(new TextBlock { Text = "Escanea el QR con la cámara del teléfono. Mantén abierta la aplicación en la PC. Al desactivar el mando, el enlace anterior deja de funcionar. Usa el mando en una red local de confianza.", Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 14, 0, 0) });

        var deck = new StackPanel(); tabs.Items.Add(new TabItem { Header = "Personalizar botones", Content = new ScrollViewer { Content = deck, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        deck.Children.Add(new TextBlock { Text = "Elige un botón para editarlo o añade un acceso directo a un himno, imagen o texto.", Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 4, 0, 12) });
        _list.ItemsSource = _buttons; _list.SelectionChanged += (_, _) => SelectButton(); deck.Children.Add(_list);
        DialogUi.Label(deck, "Nombre del botón"); deck.Children.Add(_label);
        DialogUi.Label(deck, "Acción");
        _action.ItemsSource = new[] { "Proyectar preparado", "Siguiente", "Anterior", "Pantalla negra / restaurar", "Mostrar fondo", "Pausar / continuar", "Reiniciar video", "Proyectar un contenido" };
        _action.SelectedIndex = 0; _action.SelectionChanged += (_, _) => _cue.IsEnabled = _action.SelectedIndex == 7; deck.Children.Add(_action);
        DialogUi.Label(deck, "Contenido del acceso directo"); _cue.ItemsSource = cues; _cue.DisplayMemberPath = "Title"; _cue.IsEnabled = false; deck.Children.Add(_cue);
        DialogUi.Label(deck, "Color"); _color.ItemsSource = new[] { "Turquesa", "Azul", "Ámbar", "Rosa" }; _color.SelectedIndex = 0; deck.Children.Add(_color);
        var row = new WrapPanel();
        row.Children.Add(DialogUi.Button("Guardar botón", SaveButton, true));
        var add = DialogUi.Button("Nuevo", () => { _list.SelectedItem = null; _label.Text = ""; _action.SelectedIndex = 0; _color.SelectedIndex = 0; }); add.Margin = new Thickness(8, 12, 0, 0); row.Children.Add(add);
        var remove = DialogUi.Button("Quitar", () => { if (_list.SelectedItem is DeckButton button) { _buttons.Remove(button); DeckChanged?.Invoke(_buttons.ToList()); } }); remove.Margin = new Thickness(8, 12, 0, 0); row.Children.Add(remove); deck.Children.Add(row);
        if (_buttons.Count > 0) _list.SelectedIndex = 0;
        RefreshConnection();
    }
    private async Task Start()
    {
        if (StartRequested is null) return;
        _start.IsEnabled = false; _status.Text = "Activando mando…";
        try { _host = await StartRequested(); RefreshConnection(); }
        catch (Exception ex) { _status.Text = "No se pudo activar el mando: " + ex.Message; _start.IsEnabled = true; }
    }
    private async Task Stop()
    {
        _stop.IsEnabled = false;
        try { if (StopRequested is not null) await StopRequested(); _host = null; RefreshConnection(); }
        catch (Exception ex) { _status.Text = "No se pudo cerrar el mando: " + ex.Message; _stop.IsEnabled = true; }
    }
    private void RefreshConnection()
    {
        _start.IsEnabled = _host is null; _stop.IsEnabled = _host is not null;
        _status.Text = _host is null ? "Mando desactivado. Actívalo cuando quieras vincular un teléfono." : "Mando activo. Escanea el QR para controlar el culto.";
        if (_host is null) { _url.Text = "Activa el mando para generar el enlace."; _qr.Source = null; return; }
        var address = _addresses.SelectedItem as string ?? "127.0.0.1";
        _url.Text = $"http://{address}:{_host.Port}/#token={_host.Token}";
        using var data = QRCodeGenerator.GenerateQrCode(_url.Text, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        using var stream = new MemoryStream(qr.GetGraphic(8));
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        _qr.Source = bitmap;
    }
    private void SelectButton()
    {
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
        _list.SelectedItem = button; DeckChanged?.Invoke(_buttons.ToList());
    }
}
