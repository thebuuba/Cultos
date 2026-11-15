using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Culto.Core;

namespace Culto.Desktop.Dialogs;

public sealed class MoveSettingsEditor : StackPanel
{
    private readonly ComboBox _position, _easing, _function;
    private readonly Slider _zoom, _curve;
    private bool _loading;
    public event Action? Changed;
    public MoveItemOptions Options => new() { Position = (int)(_position.SelectedValue ?? 0), Easing = (int)(_easing.SelectedValue ?? 3),
        Function = (int)(_function.SelectedValue ?? 2), Zoom = _zoom.Value, Curve = _curve.Value };

    public MoveSettingsEditor(string title, string prefix, MoveItemOptions options)
    {
        Margin = new Thickness(0, 0, 14, 0);
        Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) });
        _position = Choice("Posición", prefix + "Position", new[] { (0, "Sin desplazamiento"), (1, "Centro"), (2, "Fuera desde el centro"),
            (22, "Arriba izquierda"), (18, "Arriba centro"), (26, "Arriba derecha"), (6, "Izquierda"), (10, "Derecha"),
            (38, "Abajo izquierda"), (34, "Abajo centro"), (42, "Abajo derecha"),
            (68, "Barrido izquierdo"), (72, "Barrido derecho"), (80, "Barrido superior"), (96, "Barrido inferior") });
        _zoom = Number("Escala", prefix + "Zoom", 0, 200, 1, "%");
        _curve = Number("Curva", prefix + "Curve", -2, 2, .01, "");
        _easing = Choice("Suavizado", prefix + "Easing", new[] { (0, "Lineal"), (1, "Acelerar"), (2, "Frenar"), (3, "Acelerar y frenar") });
        _function = Choice("Función", prefix + "Function", new[] { (1, "Cuadrática"), (2, "Cúbica"), (3, "Cuártica"), (4, "Quíntica"),
            (5, "Seno"), (6, "Circular"), (7, "Exponencial"), (8, "Elástica"), (9, "Rebote"), (10, "Retroceso") });
        Load(options);
    }
    private ComboBox Choice(string label, string name, IEnumerable<(int Value, string Label)> options)
    {
        var box = new ComboBox { Name = name, SelectedValuePath = "Tag", Margin = new Thickness(0, 0, 0, 8) };
        foreach (var option in options) box.Items.Add(new ComboBoxItem { Tag = option.Value, Content = option.Label });
        DialogUi.Label(this, label, box); Children.Add(box);
        box.SelectionChanged += (_, _) => { if (!_loading) Changed?.Invoke(); }; return box;
    }
    private Slider Number(string title, string name, double min, double max, double tick, string unit)
    {
        var label = new TextBlock { Margin = new Thickness(0, 6, 0, 2) }; Children.Add(label);
        var slider = new Slider { Name = name, Minimum = min, Maximum = max, TickFrequency = tick, SmallChange = tick,
            LargeChange = tick * 10, IsSnapToTickEnabled = true, Margin = new Thickness(0, 2, 0, 8) };
        AutomationProperties.SetName(slider, title);
        slider.ValueChanged += (_, _) => { label.Text = $"{title} · {slider.Value:0.##}{unit}"; if (!_loading) Changed?.Invoke(); };
        label.Text = $"{title} · 0{unit}"; Children.Add(slider); return slider;
    }
    public void Load(MoveItemOptions options)
    {
        _loading = true;
        try { _position.SelectedValue = options.Position; _easing.SelectedValue = options.Easing; _function.SelectedValue = options.Function;
            _zoom.Value = options.Zoom; _curve.Value = options.Curve; }
        finally { _loading = false; }
    }
}
