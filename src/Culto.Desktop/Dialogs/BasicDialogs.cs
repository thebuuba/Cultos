using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop.Dialogs;

public static class DialogUi
{
    public static StackPanel Panel(Window window, string title, string description)
    {
        window.Style = (Style)Application.Current.FindResource(typeof(Window));
        window.Title = title; window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.SizeToContent = SizeToContent.Height; window.Width = 520; window.MinWidth = 420;
        window.MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 48);
        window.MaxWidth = Math.Max(420, SystemParameters.WorkArea.Width - 48);
        window.ResizeMode = ResizeMode.CanResize;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 16), Style = (Style)window.FindResource("MutedText") });
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        return panel;
    }
    public static void Label(Panel panel, string text, Control? target = null)
    {
        var label = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 5) };
        panel.Children.Add(label);
        if (target is not null) { AutomationProperties.SetName(target, text); AutomationProperties.SetLabeledBy(target, label); }
    }
    public static WrapPanel Actions(Panel panel)
    {
        var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        panel.Children.Add(row); return row;
    }
    public static Border Card(UIElement child) => new()
    {
        Background = (Brush)Application.Current.FindResource("PanelBrush"),
        BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Child = child
    };
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
        AutomationProperties.SetName(_text, label);
        panel.Children.Add(_text);
        var actions = DialogUi.Actions(panel);
        var cancel = DialogUi.Button("Cancelar", () => DialogResult = false); cancel.IsCancel = true; cancel.Margin = new Thickness(0, 0, 8, 0); actions.Children.Add(cancel);
        var accept = DialogUi.Button("Aceptar", () => { if (Value.Length > 0) DialogResult = true; }, true); accept.IsDefault = true; accept.Margin = new Thickness(0); actions.Children.Add(accept);
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
        _title = new TextBox { Text = cue.Title }; _number = new TextBox { Text = cue.HymnNumber?.ToString() ?? "" };
        var fields = new Grid(); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        var titleField = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; DialogUi.Label(titleField, "Título", _title); titleField.Children.Add(_title); fields.Children.Add(titleField);
        var numberField = new StackPanel(); DialogUi.Label(numberField, "N.º de himno (opcional)", _number); numberField.Children.Add(_number); Grid.SetColumn(numberField, 1); fields.Children.Add(numberField); panel.Children.Add(fields);
        _body = new TextBox { Text = cue.Body, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top };
        if (cue.Kind == CueKind.Text) { DialogUi.Label(panel, "Texto para proyectar", _body); panel.Children.Add(_body); }
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("AmberBrush"), Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed }; panel.Children.Add(error);
        var actions = DialogUi.Actions(panel);
        var cancel = DialogUi.Button("Cancelar", () => DialogResult = false); cancel.IsCancel = true; cancel.Margin = new Thickness(0, 0, 8, 0); actions.Children.Add(cancel);
        var save = DialogUi.Button("Guardar cambios", () =>
        {
            if (CueTitle.Length == 0) { error.Visibility = Visibility.Visible; error.Text = "Escribe un título."; _title.Focus(); return; }
            if (_number.Text.Trim().Length > 0 && HymnNumber is null) { error.Visibility = Visibility.Visible; error.Text = "El número debe ser un entero positivo."; _number.Focus(); return; }
            DialogResult = true;
        }, true); save.Margin = new Thickness(0); actions.Children.Add(save);
        Loaded += (_, _) => { _title.Focus(); _title.SelectAll(); };
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
        DialogUi.Label(panel, "Pantalla de salida", _screens);
        panel.Children.Add(_screens);
        _test = new CheckBox { Content = "Abrir como ventana de ensayo", IsChecked = displays.Count < 2 };
        if (!capture)
        {
            panel.Children.Add(_test);
            panel.Children.Add(new TextBlock { Text = "En el monitor principal siempre se abrirá una ventana de ensayo.", TextWrapping = TextWrapping.Wrap, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 8, 0, 0) });
        }
        var actions = DialogUi.Actions(panel);
        if (!capture)
        {
            var close = DialogUi.Button("Cerrar salida actual", () => { CloseOutput = true; DialogResult = true; }); close.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(close);
        }
        var cancel = DialogUi.Button("Cancelar", () => DialogResult = false); cancel.IsCancel = true; cancel.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(cancel);
        var open = DialogUi.Button(capture ? "Tomar captura" : "Abrir salida", () => { if (SelectedDisplay is not null) DialogResult = true; }, true); open.IsDefault = true; open.IsEnabled = SelectedDisplay is not null; open.Margin = new Thickness(0, 0, 0, 8); actions.Children.Add(open);
    }
}
