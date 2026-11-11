using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop.Dialogs;

public static class DialogUi
{
    public static StackPanel Panel(Window window, string title, string description)
    {
        window.Style = (Style)Application.Current.FindResource(typeof(Window));
        window.Title = title; window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.SizeToContent = SizeToContent.Height; window.Width = 500; window.ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18), Style = (Style)window.FindResource("MutedText") });
        window.Content = panel; return panel;
    }
    public static void Label(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 12, 0, 5) });
    public static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 12, 0, 0), MinHeight = 40 };
        if (primary) button.Style = (Style)Application.Current.FindResource("PrimaryButton");
        button.Click += (_, _) => action(); return button;
    }
}

public sealed class PromptDialog : Window
{
    private readonly TextBox _text;
    public string Value => _text.Text.Trim();
    private PromptDialog(string title, string label, string value)
    {
        var panel = DialogUi.Panel(this, title, label);
        _text = new TextBox { Text = value };
        panel.Children.Add(_text);
        panel.Children.Add(DialogUi.Button("Aceptar", () => { if (Value.Length > 0) DialogResult = true; }, true));
        var cancel = DialogUi.Button("Cancelar", () => DialogResult = false); cancel.IsCancel = true; panel.Children.Add(cancel);
        Loaded += (_, _) => { _text.Focus(); _text.SelectAll(); };
    }
    public static string? Ask(Window owner, string title, string label, string value)
    { var dialog = new PromptDialog(title, label, value) { Owner = owner }; return dialog.ShowDialog() == true ? dialog.Value : null; }
}

public sealed class CueEditDialog : Window
{
    private readonly TextBox _title, _number, _body;
    public string CueTitle => _title.Text.Trim();
    public int? HymnNumber => int.TryParse(_number.Text, out var number) && number > 0 ? number : null;
    public string Body => _body.Text;
    public CueEditDialog(Cue cue)
    {
        var panel = DialogUi.Panel(this, "Editar contenido", "Los cambios de biblioteca no alteran elementos ya añadidos al orden.");
        DialogUi.Label(panel, "Título"); _title = new TextBox { Text = cue.Title }; panel.Children.Add(_title);
        DialogUi.Label(panel, "Número de himno (opcional)"); _number = new TextBox { Text = cue.HymnNumber?.ToString() ?? "" }; panel.Children.Add(_number);
        _body = new TextBox { Text = cue.Body, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 200 };
        if (cue.Kind == CueKind.Text) { DialogUi.Label(panel, "Texto"); panel.Children.Add(_body); }
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }; panel.Children.Add(error);
        panel.Children.Add(DialogUi.Button("Guardar cambios", () =>
        {
            if (CueTitle.Length == 0) { error.Text = "Escribe un título."; return; }
            if (_number.Text.Trim().Length > 0 && HymnNumber is null) { error.Text = "El número debe ser un entero positivo."; return; }
            DialogResult = true;
        }, true));
    }
}

public sealed class DisplayDialog : Window
{
    private readonly ComboBox _screens;
    private readonly CheckBox _test;
    public DisplayInfo? SelectedDisplay => _screens.SelectedItem as DisplayInfo;
    public bool TestWindow => _test.IsChecked == true || SelectedDisplay?.IsPrimary == true;
    public bool CloseOutput { get; private set; }
    public DisplayDialog(IReadOnlyList<DisplayInfo> displays, string? selectedDevice, bool capture = false)
    {
        var panel = DialogUi.Panel(this, capture ? "Capturar pantalla" : "Pantalla de la iglesia",
            capture ? "La consola se minimizará durante 1,5 segundos. Se guardará una imagen de la pantalla elegida para que puedas revisarla antes de proyectar."
                : "Elige la salida HDMI que alimenta las tres pantallas. Usa Extender en Windows para conservar los controles en tu monitor.");
        _screens = new ComboBox { ItemsSource = displays, DisplayMemberPath = "Label", Margin = new Thickness(0, 0, 0, 12) };
        _screens.SelectedItem = displays.FirstOrDefault(d => d.DeviceName == selectedDevice) ?? displays.FirstOrDefault(d => !d.IsPrimary) ?? displays.FirstOrDefault();
        panel.Children.Add(_screens);
        _test = new CheckBox { Content = "Abrir como ventana de ensayo", IsChecked = displays.Count < 2 };
        if (!capture)
        {
            panel.Children.Add(_test);
            panel.Children.Add(new TextBlock { Text = "En el monitor principal siempre se abrirá una ventana de ensayo.", TextWrapping = TextWrapping.Wrap, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 8, 0, 0) });
        }
        panel.Children.Add(DialogUi.Button(capture ? "Tomar captura" : "Abrir salida", () => { if (SelectedDisplay is not null) DialogResult = true; }, true));
        if (!capture) panel.Children.Add(DialogUi.Button("Cerrar salida actual", () => { CloseOutput = true; DialogResult = true; }));
    }
}
