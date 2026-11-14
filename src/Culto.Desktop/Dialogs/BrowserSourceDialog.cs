using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using Culto.Core;
using Culto.Desktop.Projection;

namespace Culto.Desktop.Dialogs;

public sealed class BrowserSourceDialog : Window
{
    private readonly TextBox _title, _location, _width, _height, _fps, _css;
    private readonly CheckBox _local, _audio;
    private readonly TextBlock _error;
    private readonly BrowserSourceView _preview = new();
    private readonly Guid _id;
    public Cue? SelectedCue { get; private set; }

    public BrowserSourceDialog(Cue? source = null)
    {
        _id = source?.Id ?? Guid.NewGuid();
        var options = source?.Browser ?? new BrowserSourceOptions();
        var panel = DialogUi.Panel(this, source is null ? "Añadir navegador" : "Propiedades del navegador",
            "Carga un HTML local o una página web como escena. Ajusta su tamaño y comprueba el resultado antes de proyectar.");
        Width = 760;
        var previewHost = new Border { Height = 220, Background = new SolidColorBrush(Color.FromRgb(60, 63, 67)),
            BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1), Child = _preview };
        panel.Children.Add(previewHost);
        _title = new TextBox { Text = source?.Title ?? "Navegador" };
        DialogUi.Label(panel, "Nombre *", _title); panel.Children.Add(_title);
        _local = new CheckBox { Content = "Archivo HTML local", IsChecked = options.IsLocalFile, Margin = new Thickness(0, 12, 0, 4) };
        panel.Children.Add(_local);
        _location = new TextBox { Text = options.Location };
        DialogUi.Label(panel, "Archivo HTML o URL *", _location);
        var locationRow = new Grid(); locationRow.ColumnDefinitions.Add(new ColumnDefinition());
        locationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        locationRow.Children.Add(_location);
        var browse = DialogUi.Button("Examinar…", Browse); browse.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(browse, 1); locationRow.Children.Add(browse); panel.Children.Add(locationRow);
        var dimensions = new UniformGrid { Columns = 3, Margin = new Thickness(0, 4, 0, 0) };
        _width = Number(options.Width); _height = Number(options.Height); _fps = Number(options.FramesPerSecond);
        foreach (var field in new[] { ("Ancho (px) *", _width), ("Alto (px) *", _height), ("FPS *", _fps) })
        {
            var group = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            DialogUi.Label(group, field.Item1, field.Item2); group.Children.Add(field.Item2); dimensions.Children.Add(group);
        }
        panel.Children.Add(dimensions);
        _audio = new CheckBox { Content = "Permitir audio de esta fuente", IsChecked = options.AudioEnabled, Margin = new Thickness(0, 14, 0, 2) };
        panel.Children.Add(_audio);
        _css = new TextBox { Text = options.CustomCss, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Height = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas") };
        DialogUi.Label(panel, "CSS personalizado", _css); panel.Children.Add(_css);
        _error = new TextBlock { Foreground = (Brush)FindResource("AmberBrush"), TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) }; panel.Children.Add(_error);
        _preview.Error += message => { _error.Text = message; _error.Visibility = Visibility.Visible; };
        var actions = DialogUi.Actions(panel);
        AddAction(actions, "Vista previa", () => TryAction(() => _preview.SetSource(CreateCue().Browser!, true)));
        var cancel = AddAction(actions, "Cancelar", () => DialogResult = false); cancel.IsCancel = true;
        AddAction(actions, "Guardar", () => TryAction(() => { SelectedCue = CreateCue(); DialogResult = true; }), true);
        Closed += (_, _) => _preview.Dispose();
        Loaded += (_, _) => { _title.Focus(); if (options.Location.Length > 0) TryAction(() => _preview.SetSource(CreateCue().Browser!, true)); };
    }

    private static TextBox Number(int value) => new() { Text = value.ToString(CultureInfo.InvariantCulture) };
    private static int ReadNumber(TextBox box, string label)
    {
        if (!int.TryParse(box.Text.Trim(), out var value)) throw new ArgumentException(label + ": escribe un número entero.");
        return value;
    }
    public Cue CreateCue()
    {
        if (string.IsNullOrWhiteSpace(_title.Text)) throw new ArgumentException("Escribe un nombre para la fuente.");
        var options = new BrowserSourceOptions { Location = _location.Text.Trim(), IsLocalFile = _local.IsChecked == true,
            Width = ReadNumber(_width, "Ancho"), Height = ReadNumber(_height, "Alto"), FramesPerSecond = ReadNumber(_fps, "FPS"),
            CustomCss = _css.Text, AudioEnabled = _audio.IsChecked == true }.Normalize();
        options.Validate();
        return new Cue { Id = _id, Title = _title.Text.Trim(), Kind = CueKind.BrowserSource, Browser = options };
    }
    private void Browse()
    {
        var dialog = new OpenFileDialog { Title = "Elegir HTML", Filter = "Archivos HTML|*.html;*.htm", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        _local.IsChecked = true; _location.Text = dialog.FileName;
        if (_title.Text is "Navegador" or "") _title.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
    }
    private void TryAction(Action action)
    {
        try { _error.Visibility = Visibility.Collapsed; action(); }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException)
        { _error.Text = error.Message; _error.Visibility = Visibility.Visible; }
    }
    internal static Button AddAction(Panel panel, string label, Action action, bool primary = false)
    {
        var button = DialogUi.Button(label, action, primary); button.Margin = new Thickness(4, 4, 0, 0);
        panel.Children.Add(button); return button;
    }
}
