using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Cultos.App;

public partial class RemoteControlWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<bool, int, string, Task<RemoteControlStatus>> _apply;
    private readonly Func<RemoteControlStatus> _currentStatus;

    public RemoteControlWindow(
        AppSettings settings,
        Func<bool, int, string, Task<RemoteControlStatus>> apply,
        Func<RemoteControlStatus> currentStatus)
    {
        InitializeComponent();
        _settings = settings;
        _apply = apply;
        _currentStatus = currentStatus;

        EnabledBox.IsChecked = settings.RemoteControlEnabled;
        PortBox.Text = settings.RemoteControlPort.ToString();
        PinBox.Text = string.IsNullOrWhiteSpace(settings.RemoteControlPin)
            ? RemoteControlServer.GeneratePin()
            : settings.RemoteControlPin;

        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var status = _currentStatus();
        StatusText.Text = status.IsRunning
            ? "Activo · listo para conexiones"
            : status.Error is null
                ? "Detenido"
                : "Error · " + status.Error;

        UrlText.Text = status.IsRunning ? status.Url : "El servidor está detenido";
    }

    private void RegeneratePin_Click(object sender, RoutedEventArgs e)
    {
        PinBox.Text = RemoteControlServer.GeneratePin();
        PinBox.Focus();
        PinBox.SelectAll();
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text.Trim(), out var port) || port is < 1024 or > 65535)
        {
            MessageBox.Show(
                "Escribe un puerto entre 1024 y 65535.",
                "Control remoto",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var pin = PinBox.Text.Trim();
        if (pin.Length != 6 || !pin.All(char.IsDigit))
        {
            MessageBox.Show(
                "El PIN debe tener exactamente 6 dígitos.",
                "Control remoto",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        IsEnabled = false;
        try
        {
            var status = await _apply(EnabledBox.IsChecked == true, port, pin);
            StatusText.Text = status.IsRunning
                ? "Activo · listo para conexiones"
                : status.Error is null
                    ? "Detenido"
                    : "Error · " + status.Error;
            UrlText.Text = status.IsRunning ? status.Url : "El servidor está detenido";

            if (!status.IsRunning && EnabledBox.IsChecked == true && status.Error is not null)
            {
                MessageBox.Show(
                    "No se pudo iniciar el control remoto. " + status.Error,
                    "Control remoto",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        var status = _currentStatus();
        if (!status.IsRunning)
        {
            MessageBox.Show(
                "Activa primero el control remoto.",
                "Control remoto",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        System.Windows.Clipboard.SetText(status.Url);
        StatusText.Text = "Dirección copiada al portapapeles";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
