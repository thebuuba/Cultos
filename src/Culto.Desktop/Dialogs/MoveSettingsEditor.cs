using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Media;
using Culto.Core;

namespace Culto.Desktop.Dialogs;

public sealed class MoveSettingsEditor : StackPanel
{
    private readonly ComboBox _position, _easing, _function;
    private readonly Slider _zoom, _curve;
    private readonly Dictionary<int, Button> _positions = new(), _easings = new();
    private readonly MoveEasingGraph _graph;
    private readonly TextBlock _positionLabel;
    private readonly Brush _accent;
    private bool _loading;
    public event Action? Changed;
    public MoveItemOptions Options => new() { Position = (int)(_position.SelectedValue ?? 0), Easing = (int)(_easing.SelectedValue ?? 3),
        Function = (int)(_function.SelectedValue ?? 2), Zoom = _zoom.Value, Curve = _curve.Value };

    public MoveSettingsEditor(string title, string prefix, MoveItemOptions options)
    {
        var incoming = prefix == "MoveIn";
        _accent = Brush(incoming ? "#BEF264" : "#B18CF2");
        Margin = incoming ? new Thickness(0, 0, 8, 0) : new Thickness(8, 0, 0, 0);
        System.Windows.Documents.TextElement.SetFontFamily(this, new FontFamily("Segoe UI"));
        var content = new StackPanel();
        Children.Add(new Border { Background = Brush("#262626"), BorderBrush = Brush("#393939"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24), Padding = new Thickness(22), Child = content });
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 22) };
        var chip = new Border { Background = Brush("#353535"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 4, 10, 4),
            Child = Text(incoming ? "Entrada" : "Salida", 11, _accent) };
        DockPanel.SetDock(chip, Dock.Right); header.Children.Add(chip);
        var heading = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        heading.Children.Add(new Border { Width = 9, Height = 9, Background = _accent, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 0, 9, 0) });
        heading.Children.Add(Text(title, 15, Brushes.White, FontWeights.SemiBold));
        header.Children.Add(heading); content.Children.Add(header);

        _position = Choice(prefix + "Position", new[] { (0, "Sin desplazamiento"), (1, "Centro"), (2, "Fuera desde el centro"),
            (22, "Arriba izquierda"), (18, "Arriba centro"), (26, "Arriba derecha"), (6, "Izquierda"), (10, "Derecha"),
            (38, "Abajo izquierda"), (34, "Abajo centro"), (42, "Abajo derecha"),
            (68, "Barrido izquierdo"), (72, "Barrido derecho"), (80, "Barrido superior"), (96, "Barrido inferior") });
        _easing = Choice(prefix + "Easing", new[] { (0, "Lineal"), (1, "Acelerar"), (2, "Frenar"), (3, "Acelerar y frenar") });
        _easing.Visibility = Visibility.Collapsed; content.Children.Add(_easing);
        _function = Choice(prefix + "Function", new[] { (1, "Cuadrática"), (2, "Cúbica"), (3, "Cuártica"), (4, "Quíntica"),
            (5, "Seno"), (6, "Circular"), (7, "Exponencial"), (8, "Elástica"), (9, "Rebote"), (10, "Retroceso") });
        _graph = new MoveEasingGraph { Accent = _accent, Height = 172, Margin = new Thickness(0, 0, 0, 8) };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        var positionPanel = new StackPanel(); positionPanel.Children.Add(Label("Posición"));
        var positions = new UniformGrid { Columns = 3, Rows = 3, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var (value, symbol, label) in new[] { (22, "↖", "Arriba izquierda"), (18, "↑", "Arriba centro"), (26, "↗", "Arriba derecha"),
            (6, "←", "Izquierda"), (1, "•", "Centro"), (10, "→", "Derecha"),
            (38, "↙", "Abajo izquierda"), (34, "↓", "Abajo centro"), (42, "↘", "Abajo derecha") })
        {
            var button = MakeButton(symbol, prefix + "Position" + value, label);
            button.Height = 50; button.Padding = new Thickness(0); button.FontSize = 20; button.Margin = new Thickness(2);
            button.Click += (_, _) => _position.SelectedValue = value;
            _positions.Add(value, button); positions.Children.Add(button);
        }
        _positionLabel = Text("", 11, _accent); _positionLabel.Margin = new Thickness(0, 0, 0, 8);
        _positionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        positionPanel.Children.Add(positions); positionPanel.Children.Add(_positionLabel); positionPanel.Children.Add(_position); top.Children.Add(positionPanel);
        var easingPanel = new StackPanel(); easingPanel.Children.Add(Label("Curva de tiempo")); easingPanel.Children.Add(_graph);
        Grid.SetColumn(easingPanel, 2); top.Children.Add(easingPanel); content.Children.Add(top);
        _zoom = Number(content, "Escala", prefix + "Zoom", 0, 200, 1, "%", "Desde un punto", "200%");
        _curve = Number(content, "Curva", prefix + "Curve", -2, 2, .01, "", "Arco hacia un lado", "Arco al otro"); _curve.Tag = "Centered";
        content.Children.Add(new Border { Height = 1, Background = Brush("#393939"), Margin = new Thickness(0, 16, 0, 16) });
        content.Children.Add(Label("Suavizado"));
        var modes = new UniformGrid { Columns = 3 };
        foreach (var (value, label) in new[] { (1, "Acelerar"), (2, "Frenar"), (3, "Acelerar y frenar") })
        {
            var button = MakeButton(label, prefix + "Easing" + value, value == 3 ? "Acelerar y frenar" : label);
            button.Padding = new Thickness(4, 8, 4, 8); button.FontSize = 11; button.Margin = new Thickness(0, 0, value == 3 ? 0 : 4, 0);
            button.Click += (_, _) => _easing.SelectedValue = value;
            _easings.Add(value, button); modes.Children.Add(button);
        }
        content.Children.Add(modes);
        var linear = MakeButton("Lineal", prefix + "Easing0", "Sin suavizado, velocidad constante");
        linear.HorizontalAlignment = HorizontalAlignment.Left; linear.Margin = new Thickness(0, 6, 0, 0);
        linear.Padding = new Thickness(10, 4, 10, 4); linear.FontSize = 11; linear.Click += (_, _) => _easing.SelectedValue = 0;
        _easings.Add(0, linear); content.Children.Add(linear);
        var functionLabel = Label("Función"); functionLabel.Margin = new Thickness(0, 16, 0, 8); content.Children.Add(functionLabel);
        content.Children.Add(_function);
        _position.SelectionChanged += (_, _) => SelectionChanged();
        _easing.SelectionChanged += (_, _) => SelectionChanged();
        _function.SelectionChanged += (_, _) => SelectionChanged();
        Load(options);
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static TextBlock Text(string text, double size, Brush foreground, FontWeight? weight = null) =>
        new() { Text = text, FontSize = size, Foreground = foreground, FontWeight = weight ?? FontWeights.Normal };
    private static TextBlock Label(string text) => new() { Text = text, FontSize = 12, Foreground = Brush("#C5C5C5"), Margin = new Thickness(0, 0, 0, 10) };
    private static Button MakeButton(string text, string name, string accessibleName)
    {
        var button = new Button { Content = text, Name = name, Foreground = Brush("#AFAFAF"), Background = Brush("#303030"), BorderBrush = Brush("#414141"), BorderThickness = new Thickness(1) };
        button.SetResourceReference(StyleProperty, "EditorButton");
        AutomationProperties.SetName(button, accessibleName); button.ToolTip = accessibleName; return button;
    }
    private static ComboBox Choice(string name, IEnumerable<(int Value, string Label)> options)
    {
        var box = new ComboBox { Name = name, SelectedValuePath = "Tag", FontSize = 12, Margin = new Thickness(0), MinWidth = 0,
            Background = Brush("#303030"), Foreground = Brushes.WhiteSmoke,
            HorizontalContentAlignment = HorizontalAlignment.Stretch };
        foreach (var option in options) box.Items.Add(new ComboBoxItem { Tag = option.Value, Content = option.Label });
        AutomationProperties.SetName(box, name.EndsWith("Position") ? "Posición" : name.EndsWith("Function") ? "Función de suavizado" : "Suavizado");
        return box;
    }
    private Slider Number(Panel parent, string title, string name, double min, double max, double tick, string unit, string left, string right)
    {
        var heading = new DockPanel { Margin = new Thickness(0, 18, 0, 8) };
        var value = Text("", 11, Brushes.White, FontWeights.SemiBold);
        var pill = new Border { Background = Brush("#363636"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3, 8, 3), Child = value };
        DockPanel.SetDock(pill, Dock.Right); heading.Children.Add(pill); heading.Children.Add(Text(title, 12, Brush("#D2D2D2"))); parent.Children.Add(heading);
        var slider = new Slider { Name = name, Minimum = min, Maximum = max, TickFrequency = tick, SmallChange = tick,
            LargeChange = tick * 10, IsSnapToTickEnabled = true, Foreground = _accent, Margin = new Thickness(0, 0, 0, 6) };
        slider.SetResourceReference(StyleProperty, "EditorSlider"); AutomationProperties.SetName(slider, title);
        slider.ValueChanged += (_, _) => { value.Text = $"{slider.Value:0.##}{unit}"; if (!_loading) Changed?.Invoke(); };
        value.Text = $"0{unit}"; parent.Children.Add(slider);
        var captions = new Grid(); captions.Children.Add(Text(left, 10, Brush("#999999")));
        var end = Text(right, 10, Brush("#999999")); end.HorizontalAlignment = HorizontalAlignment.Right; captions.Children.Add(end);
        if (title == "Escala") { var middle = Text("Tamaño real · 100%", 10, Brush("#999999")); middle.HorizontalAlignment = HorizontalAlignment.Center; captions.Children.Add(middle); }
        parent.Children.Add(captions); return slider;
    }
    private void SelectionChanged() { UpdateSelection(); if (!_loading) Changed?.Invoke(); }
    private void UpdateSelection()
    {
        foreach (var (value, button) in _positions) Select(button, Equals(_position.SelectedValue, value), _accent);
        foreach (var (value, button) in _easings) Select(button, Equals(_easing.SelectedValue, value), Brushes.White);
        _positionLabel.Text = (_position.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        _graph.Mode = (int)(_easing.SelectedValue ?? 3); _graph.Function = (int)(_function.SelectedValue ?? 2); _graph.InvalidateVisual();
    }
    private void Select(Button button, bool selected, Brush selectedBrush)
    {
        button.Background = selected ? selectedBrush : Brush("#303030"); button.Foreground = selected ? Brush("#202020") : Brush("#B7B7B7");
        button.BorderBrush = selected ? selectedBrush : Brush("#414141"); AutomationProperties.SetItemStatus(button, selected ? "Seleccionado" : "");
    }
    public void Load(MoveItemOptions options)
    {
        _loading = true;
        try { _position.SelectedValue = options.Position; _easing.SelectedValue = options.Easing; _function.SelectedValue = options.Function;
            _zoom.Value = options.Zoom; _curve.Value = options.Curve; UpdateSelection(); }
        finally { _loading = false; }
    }
}
