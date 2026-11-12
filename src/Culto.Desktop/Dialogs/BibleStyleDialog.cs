using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;
using Microsoft.Win32;

namespace Culto.Desktop.Dialogs;

public sealed class BibleStyleDialog : Window
{
    private BibleVisualStyle _draft;
    private readonly Cue _example;
    private readonly ProjectionSurface _preview = new(preview: false) { Width = 1600, Height = 900 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBlock _imageName = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBox _textColor;
    private readonly TextBox _backgroundColor;
    private readonly Slider _duration;
    private bool _ready;

    public BibleVisualStyle EditedStyle => _draft.Normalize();

    public BibleStyleDialog(BibleVisualStyle style, Cue example, TransitionOptions? transitions = null)
    {
        _preview.Transitions = (transitions ?? new()).Normalize();
        _draft = (style with { }).Normalize();
        // A Bible-only copy keeps the editor isolated from live content and media playback.
        _example = new Cue { Kind = CueKind.Bible, Body = example.Body, Reference = example.Reference, Version = example.Version, Title = example.Title };
        base.Style = (System.Windows.Style)Application.Current.FindResource(typeof(Window));
        Title = "Estilo de la Biblia";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        MaxWidth = Math.Max(1, SystemParameters.WorkArea.Width - 32);
        MaxHeight = Math.Max(1, SystemParameters.WorkArea.Height - 32);
        Width = Math.Min(1000, MaxWidth); Height = Math.Min(700, MaxHeight);
        MinWidth = Math.Min(760, MaxWidth); MinHeight = Math.Min(560, MaxHeight);

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = Title, FontSize = 25, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 20) });

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetRow(body, 1); root.Children.Add(body);
        var controls = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        body.Children.Add(new ScrollViewer { Content = controls, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

        AddHeading(controls, "TEXTO");
        var family = new ComboBox { Name = "BibleFontFamily", MaxDropDownHeight = 260 };
        foreach (var name in Fonts.SystemFontFamilies.Select(font => font.Source).Append(_draft.FontFamily).Distinct().OrderBy(name => name)) family.Items.Add(name);
        family.SelectedItem = _draft.FontFamily;
        family.SelectionChanged += (_, _) => { if (family.SelectedItem is string name) { _draft.FontFamily = name; RefreshPreview(); } };
        AddField(controls, "Fuente", family);
        AddField(controls, "Tamaño", CreateSlider("BibleFontSize", 28, 120, _draft.FontSize, 1, value => { _draft.FontSize = value; RefreshPreview(); }, "0"));
        var emphasis = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        var bold = new CheckBox { Name = "BibleBold", Content = "Negrita", IsChecked = _draft.Bold, Margin = new Thickness(0, 0, 18, 0) };
        var italic = new CheckBox { Name = "BibleItalic", Content = "Cursiva", IsChecked = _draft.Italic };
        bold.Checked += (_, _) => { _draft.Bold = true; RefreshPreview(); };
        bold.Unchecked += (_, _) => { _draft.Bold = false; RefreshPreview(); };
        italic.Checked += (_, _) => { _draft.Italic = true; RefreshPreview(); };
        italic.Unchecked += (_, _) => { _draft.Italic = false; RefreshPreview(); };
        emphasis.Children.Add(bold); emphasis.Children.Add(italic); controls.Children.Add(emphasis);
        _textColor = ColorField(controls, "Color del texto", "BibleTextColor", _draft.TextColor, value => _draft.TextColor = value);
        AddField(controls, "Alineación", Choice("BibleAlignment", _draft.Alignment, [("Left", "Izquierda"), ("Center", "Centro"), ("Right", "Derecha")], value => _draft.Alignment = value));
        AddField(controls, "Efecto", Choice("BibleTextEffect", _draft.TextEffect, [("None", "Sin efecto"), ("Shadow", "Sombra"), ("Glow", "Resplandor")], value => _draft.TextEffect = value));

        AddHeading(controls, "FONDO");
        _backgroundColor = ColorField(controls, "Color del fondo", "BibleBackgroundColor", _draft.BackgroundColor, value => _draft.BackgroundColor = value);
        _imageName.Foreground = (Brush)FindResource("MutedBrush");
        _imageName.Margin = new Thickness(0, 0, 0, 8); controls.Children.Add(_imageName);
        var imageActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        imageActions.Children.Add(Action("Elegir imagen", "ChooseBibleImageButton", ChooseImage));
        imageActions.Children.Add(Action("Quitar", "ClearBibleImageButton", () => { _draft.BackgroundImagePath = null; UpdateImageName(); SetStatus(""); RefreshPreview(); }));
        controls.Children.Add(imageActions);
        AddField(controls, "Oscurecer imagen", CreateSlider("BibleBackgroundDim", 0, .9, _draft.BackgroundDim, .05, value => { _draft.BackgroundDim = value; RefreshPreview(); }, "P0"));

        AddHeading(controls, "TRANSICIÓN");
        var transition = Choice("BibleTransition", _draft.Transition, [("System", "Del sistema"), ("Cut", "Corte"), ("Dissolve", "Disolver")], value => { _draft.Transition = value; UpdateDurationState(); });
        AddField(controls, "Entrada del versículo", transition);
        var durationField = CreateSlider("BibleTransitionDuration", 100, 2000, _draft.TransitionMilliseconds, 50, value => { _draft.TransitionMilliseconds = (int)value; RefreshPreview(); }, "0' ms'");
        _duration = (Slider)((DockPanel)durationField).Children[1];
        AddField(controls, "Duración", durationField);

        var previewPanel = new Grid();
        previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        previewPanel.RowDefinitions.Add(new RowDefinition());
        previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        previewPanel.Children.Add(new TextBlock { Text = "VISTA PREVIA", Style = (System.Windows.Style)FindResource("Overline") });
        var previewFrame = new Border
        {
            Background = Brushes.Black, BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(8), VerticalAlignment = VerticalAlignment.Center,
            Child = new Viewbox { Stretch = Stretch.Uniform, Child = _preview }
        };
        Grid.SetRow(previewFrame, 1); previewPanel.Children.Add(previewFrame);
        var previewActions = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        var test = Action("Probar transición", "TestBibleTransitionButton", () => { _preview.ShowCue(null); _preview.TransitionTo(_example); });
        test.HorizontalAlignment = HorizontalAlignment.Left; previewActions.Children.Add(test);
        _status.Margin = new Thickness(0, 12, 0, 0); _status.Foreground = (Brush)FindResource("AmberBrush");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        previewActions.Children.Add(_status); Grid.SetRow(previewActions, 2); previewPanel.Children.Add(previewActions);
        Grid.SetColumn(previewPanel, 2); body.Children.Add(previewPanel);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancel = Action("Cancelar", "CancelBibleStyleButton", () => DialogResult = false); cancel.IsCancel = true;
        var save = Action("Guardar", "SaveBibleStyleButton", Save); save.IsDefault = true; save.Style = (System.Windows.Style)FindResource("PrimaryButton");
        actions.Children.Add(cancel); actions.Children.Add(save); Grid.SetRow(actions, 2); root.Children.Add(actions);
        Content = root;
        Closed += (_, _) => _preview.Dispose();
        if (!string.IsNullOrWhiteSpace(_draft.BackgroundImagePath) && !CanReadImage(_draft.BackgroundImagePath))
        {
            _draft.BackgroundImagePath = null;
            SetStatus("La imagen anterior no está disponible. Se usa el color de fondo.");
        }
        UpdateImageName(); _ready = true; UpdateDurationState(); RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (!_ready) return;
        _preview.BibleStyle = _draft.Normalize();
        _preview.ShowCue(_example);
    }

    private void UpdateDurationState()
    {
        if (!_ready) return;
        _duration.IsEnabled = _draft.Transition == "Dissolve";
        RefreshPreview();
    }

    private void ChooseImage()
    {
        var picker = new OpenFileDialog { Title = "Fondo de la Biblia", CheckFileExists = true, Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.wdp" };
        if (picker.ShowDialog(this) != true) return;
        if (!CanReadImage(picker.FileName)) { SetStatus("No se pudo abrir esa imagen. Elige otra imagen."); return; }
        _draft.BackgroundImagePath = picker.FileName; SetStatus(""); UpdateImageName(); RefreshPreview();
    }

    private void Save()
    {
        if (!ValidateColor(_textColor, "texto") || !ValidateColor(_backgroundColor, "fondo")) return;
        if (!string.IsNullOrWhiteSpace(_draft.BackgroundImagePath) && !CanReadImage(_draft.BackgroundImagePath))
        {
            _draft.BackgroundImagePath = null; UpdateImageName(); RefreshPreview();
            SetStatus("La imagen ya no está disponible. Se usa el color de fondo; revisa y vuelve a guardar.");
            return;
        }
        _draft = _draft.Normalize();
        DialogResult = true;
    }

    private bool ValidateColor(TextBox field, string label)
    {
        if (BibleVisualStyle.ValidColor(field.Text.Trim())) return true;
        SetStatus($"Revisa el color del {label}: usa #RRGGBB."); field.Focus(); field.SelectAll(); return false;
    }

    private TextBox ColorField(Panel parent, string title, string name, string value, Action<string> update)
    {
        var row = new DockPanel();
        var field = new TextBox { Name = name, Text = value, MaxLength = 7, MinWidth = 100, VerticalContentAlignment = VerticalAlignment.Center };
        var pick = Action("Elegir", name + "Picker", () =>
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            if (BibleVisualStyle.ValidColor(field.Text.Trim())) dialog.Color = System.Drawing.ColorTranslator.FromHtml(field.Text.Trim());
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (dialog.ShowDialog(new NativeOwner(handle)) == System.Windows.Forms.DialogResult.OK)
                field.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        });
        pick.Margin = new Thickness(8, 0, 0, 0); DockPanel.SetDock(pick, Dock.Right); row.Children.Add(pick); row.Children.Add(field);
        field.TextChanged += (_, _) =>
        {
            var valid = BibleVisualStyle.ValidColor(field.Text.Trim());
            field.BorderBrush = (Brush)FindResource(valid ? "BorderBrush" : "AmberBrush");
            if (valid) { update(field.Text.Trim()); RefreshPreview(); }
        };
        AutomationProperties.SetName(field, title); AutomationProperties.SetName(pick, "Elegir " + title.ToLowerInvariant());
        AddField(parent, title, row); return field;
    }

    private ComboBox Choice(string name, string selected, (string Value, string Label)[] options, Action<string> update)
    {
        var combo = new ComboBox { Name = name, SelectedValuePath = "Tag" };
        foreach (var option in options) combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
        combo.SelectedValue = selected;
        combo.SelectionChanged += (_, _) => { if (combo.SelectedValue is string value) { update(value); RefreshPreview(); } };
        return combo;
    }

    private static FrameworkElement CreateSlider(string name, double min, double max, double value, double step, Action<double> update, string format)
    {
        var row = new DockPanel();
        var number = new TextBlock { Text = value.ToString(format), MinWidth = 58, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
        var slider = new Slider { Name = name, Minimum = min, Maximum = max, Value = value, TickFrequency = step, SmallChange = step, LargeChange = step * 2, IsSnapToTickEnabled = true };
        AutomationProperties.SetName(slider, name switch { "BibleFontSize" => "Tamaño de fuente", "BibleBackgroundDim" => "Oscurecer imagen", _ => "Duración de transición" });
        slider.ValueChanged += (_, _) => { number.Text = slider.Value.ToString(format); update(slider.Value); };
        row.Children.Add(slider); return row;
    }

    private void AddHeading(Panel parent, string title) => parent.Children.Add(new TextBlock { Text = title, Style = (System.Windows.Style)FindResource("Overline"), Margin = new Thickness(0, 10, 0, 10) });

    private static void AddField(Panel parent, string title, FrameworkElement control)
    {
        parent.Children.Add(new TextBlock { Text = title, FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
        control.Margin = new Thickness(0, 0, 0, 12); AutomationProperties.SetName(control, title); parent.Children.Add(control);
    }

    private static Button Action(string title, string name, Action action)
    {
        var button = new Button { Name = name, Content = title, Padding = new Thickness(13, 8, 13, 8), Margin = new Thickness(0, 0, 8, 0) };
        button.Click += (_, _) => action(); return button;
    }

    private void UpdateImageName()
    {
        _imageName.Text = string.IsNullOrWhiteSpace(_draft.BackgroundImagePath) ? "Sin imagen" : Path.GetFileName(_draft.BackgroundImagePath);
        _imageName.ToolTip = _draft.BackgroundImagePath;
    }

    private void SetStatus(string text) => _status.Text = text;

    private static bool CanReadImage(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 1600; image.StreamSource = stream; image.EndInit(); image.Freeze();
            return image.PixelWidth > 0 && image.PixelHeight > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException or System.Security.SecurityException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private sealed class NativeOwner(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle => handle;
    }
}
