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
using static Culto.Desktop.Dialogs.TransitionEditorUi;

namespace Culto.Desktop.Dialogs;

public sealed partial class BibleStyleDialog : Window
{
    private BibleVisualStyle _draft;
    private readonly BibleVisualStyle _savedStyle;
    private readonly Cue _example;
    private readonly ProjectionSurface _preview = new(preview: false) { Width = 1600, Height = 900 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBlock _imageName = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBox _textColor;
    private readonly TextBox _referenceColor;
    private readonly TextBox _backgroundColor;
    private readonly Slider _duration;
    private readonly ListBox _sections;
    private bool _ready;

    public BibleVisualStyle EditedStyle => _draft.Normalize();
    public Func<BibleVisualStyle, bool>? PersistStyle { private get; init; }

    public BibleStyleDialog(BibleVisualStyle style, Cue example, TransitionOptions? transitions = null)
    {
        _preview.Transitions = (transitions ?? new()).Normalize();
        _draft = (style with { }).Normalize();
        _savedStyle = _draft with { };
        // A Bible-only copy keeps the editor isolated from live content and media playback.
        _example = new Cue { Kind = CueKind.Bible, Body = example.Body, Reference = example.Reference, Version = example.Version, Title = example.Title };
        base.Style = (System.Windows.Style)Application.Current.FindResource(typeof(Window));
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/CultoStudio;component/Dialogs/TransitionEditorTheme.xaml", UriKind.Relative) });
        Background = Brush("#191919");
        Title = "Estilo de la Biblia";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        MaxWidth = Math.Max(1, SystemParameters.WorkArea.Width - 32);
        MaxHeight = Math.Max(1, SystemParameters.WorkArea.Height - 32);
        Width = Math.Min(1180, MaxWidth); Height = Math.Min(780, MaxHeight);
        MinWidth = Math.Min(900, MaxWidth); MinHeight = Math.Min(600, MaxHeight);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new StackPanel { Margin = new Thickness(24, 22, 24, 20) };
        header.Children.Add(Text(Title, 26, bold: true));
        header.Children.Add(Text("Personaliza el texto, la referencia y el fondo de tus pasajes.", 12, Muted));
        root.Children.Add(header);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetRow(body, 1); root.Children.Add(body);
        var sections = new ListBox { Name = "BibleStyleSections", Margin = new Thickness(12), Background = Brushes.Transparent,
            ItemContainerStyle = (System.Windows.Style)FindResource("EditorPresetItem") };
        _sections = sections;
        AutomationProperties.SetName(sections, "Secciones del estilo de la Biblia");
        var sidebar = new Border { Background = Brush("#202020"), BorderBrush = Brush("#353535"), BorderThickness = new Thickness(0, 0, 1, 0), Child = sections };
        body.Children.Add(sidebar);
        var sectionHost = new Grid();
        var sectionPanels = new System.Collections.Generic.Dictionary<string, Border>();
        StackPanel Section(string key, string title, string heading)
        {
            sections.Items.Add(new ListBoxItem { Tag = key, Content = Text(title, 13, bold: true), Margin = new Thickness(0, 0, 0, 6) });
            var panel = new StackPanel();
            var label = Text(heading, 18, bold: true); label.Margin = new Thickness(0, 0, 0, 18); panel.Children.Add(label);
            var card = Card(panel); card.VerticalAlignment = VerticalAlignment.Top;
            card.Visibility = Visibility.Collapsed; sectionHost.Children.Add(card); sectionPanels.Add(key, card);
            return panel;
        }
        var controlsScroll = new ScrollViewer { Content = sectionHost, Margin = new Thickness(18, 0, 18, 18),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(controlsScroll, 1); body.Children.Add(controlsScroll);

        var controls = Section("texto", "Texto", "Texto del versículo");
        var fontNames = Fonts.SystemFontFamilies.Select(font => font.Source).Append(_draft.FontFamily)
            .Append(_draft.ReferenceFontFamily ?? _draft.FontFamily).Distinct().OrderBy(name => name).ToArray();
        var family = new ComboBox { Name = "BibleFontFamily", MaxDropDownHeight = 260 };
        foreach (var name in fontNames) family.Items.Add(name);
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

        controls = Section("referencia", "Referencia", "Libro y referencia");
        const string sameFont = "Misma fuente del texto";
        var referenceFamily = new ComboBox { Name = "BibleReferenceFontFamily", MaxDropDownHeight = 260 };
        referenceFamily.Items.Add(sameFont);
        foreach (var name in fontNames) referenceFamily.Items.Add(name);
        referenceFamily.SelectedItem = _draft.ReferenceFontFamily ?? sameFont;
        referenceFamily.SelectionChanged += (_, _) =>
        {
            _draft.ReferenceFontFamily = referenceFamily.SelectedItem is string name && name != sameFont ? name : null;
            RefreshPreview();
        };
        AddField(controls, "Fuente de la referencia", referenceFamily);
        controls.Children.Add(Text("Se aplica al libro, capítulo y versículo, como «Génesis 1:5».", 12, Muted));
        var sameColor = new CheckBox { Name = "BibleReferenceSameColor", Content = "Usar el color del texto",
            IsChecked = _draft.ReferenceColor is null, Margin = new Thickness(0, 16, 0, 12) };
        AutomationProperties.SetName(sameColor, "Usar el color del texto en la referencia");
        controls.Children.Add(sameColor);
        _referenceColor = ColorField(controls, "Color de la referencia", "BibleReferenceColor",
            _draft.ReferenceColor ?? _draft.TextColor, value => { if (sameColor.IsChecked != true) _draft.ReferenceColor = value; });
        var referenceColorRow = (FrameworkElement)_referenceColor.Parent;
        referenceColorRow.IsEnabled = sameColor.IsChecked != true;
        sameColor.Checked += (_, _) =>
        {
            _draft.ReferenceColor = null; referenceColorRow.IsEnabled = false; RefreshPreview();
        };
        sameColor.Unchecked += (_, _) =>
        {
            referenceColorRow.IsEnabled = true;
            _draft.ReferenceColor = BibleVisualStyle.ValidColor(_referenceColor.Text.Trim())
                ? _referenceColor.Text.Trim() : _draft.TextColor;
            RefreshPreview();
        };
        var showVersion = new CheckBox { Name = "BibleShowVersion", Content = "Mostrar versión de la Biblia",
            IsChecked = _draft.ShowVersion, Margin = new Thickness(0, 8, 0, 0) };
        AutomationProperties.SetName(showVersion, "Mostrar versión de la Biblia");
        showVersion.Checked += (_, _) => { _draft.ShowVersion = true; RefreshPreview(); };
        showVersion.Unchecked += (_, _) => { _draft.ShowVersion = false; RefreshPreview(); };
        controls.Children.Add(showVersion);

        controls = Section("fondo", "Fondo", "Fondo del pasaje");
        _backgroundColor = ColorField(controls, "Color del fondo", "BibleBackgroundColor", _draft.BackgroundColor, value => _draft.BackgroundColor = value);
        _imageName.Foreground = (Brush)FindResource("MutedBrush");
        _imageName.Margin = new Thickness(0, 0, 0, 8); controls.Children.Add(_imageName);
        var imageActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        imageActions.Children.Add(Action("Elegir imagen", "ChooseBibleImageButton", ChooseImage));
        imageActions.Children.Add(Action("Quitar", "ClearBibleImageButton", () => { _draft.BackgroundImagePath = null; UpdateImageName(); SetStatus(""); RefreshPreview(); }));
        controls.Children.Add(imageActions);
        AddField(controls, "Oscurecer imagen", CreateSlider("BibleBackgroundDim", 0, .9, _draft.BackgroundDim, .05, value => { _draft.BackgroundDim = value; RefreshPreview(); }, "P0"));

        controls = Section("transicion", "Transición", "Texto y referencia");
        CreateTransitionSelector(controls);
        var durationField = CreateSlider("BibleTransitionDuration", 100, 2000, _draft.TransitionMilliseconds, 50, value => { _draft.TransitionMilliseconds = (int)value; RefreshPreview(); }, "0' ms'");
        _duration = (Slider)((DockPanel)durationField).Children[1];
        AddField(controls, "Duración de ambas", durationField);

        var previewPanel = new Grid();
        previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        previewPanel.RowDefinitions.Add(new RowDefinition());
        previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        previewPanel.Children.Add(Text("Vista previa", 17, bold: true));
        var previewFrame = new Border
        {
            Background = Brushes.Black, BorderBrush = Brush("#353535"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(6), Margin = new Thickness(0, 16, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Child = new Viewbox { Stretch = Stretch.Uniform, Child = _preview }
        };
        Grid.SetRow(previewFrame, 1); previewPanel.Children.Add(previewFrame);
        var previewActions = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        var test = Action("Probar transición", "TestBibleTransitionButton", () => { _preview.ShowCue(null); _preview.TransitionTo(_example); });
        test.HorizontalAlignment = HorizontalAlignment.Left; previewActions.Children.Add(test);
        _status.Margin = new Thickness(0, 12, 0, 0); _status.Foreground = (Brush)FindResource("AmberBrush");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        previewActions.Children.Add(_status); Grid.SetRow(previewActions, 2); previewPanel.Children.Add(previewActions);
        var previewCard = Card(previewPanel);
        previewCard.Margin = new Thickness(0, 0, 22, 18);
        Grid.SetColumn(previewCard, 2); body.Children.Add(previewCard);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(22, 14, 14, 14) };
        var cancel = Action("Cancelar", "CancelBibleStyleButton", () => DialogResult = false); cancel.IsCancel = true;
        var save = Action("Guardar", "SaveBibleStyleButton", Save); save.IsDefault = true; save.Background = Lime; save.Foreground = Brush("#171717");
        actions.Children.Add(cancel); actions.Children.Add(save);
        var footer = new Border { Background = Brush("#242424"), BorderBrush = Brush("#353535"), BorderThickness = new Thickness(0, 1, 0, 0), Child = actions };
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = root;
        sections.SelectionChanged += (_, _) =>
        {
            if (sections.SelectedItem is not ListBoxItem { Tag: string key }) return;
            foreach (var entry in sectionPanels) entry.Value.Visibility = entry.Key == key ? Visibility.Visible : Visibility.Collapsed;
            controlsScroll.ScrollToTop();
        };
        sections.SelectedIndex = 0;
        Closing += (_, e) =>
        {
            if (DialogResult is not null || !_ready) return;
            if (EditedStyle == _savedStyle && BibleVisualStyle.ValidColor(_textColor.Text.Trim())
                && (!_referenceColor.IsEnabled || BibleVisualStyle.ValidColor(_referenceColor.Text.Trim()))
                && BibleVisualStyle.ValidColor(_backgroundColor.Text.Trim())) return;
            e.Cancel = true;
            SetStatus("Tienes cambios sin guardar. Pulsa Guardar para conservarlos o Cancelar para descartarlos.");
        };
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
        _duration.IsEnabled = _draft.Transition == "Dissolve" || BibleTextTransitionCatalog.TryGet(_draft.Transition, out _)
            || _draft.ReferenceTransition == "Dissolve" || BibleTextTransitionCatalog.TryGet(_draft.ReferenceTransition, out _);
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
        if (!ValidateColor(_textColor, "texto")
            || _referenceColor.IsEnabled && !ValidateColor(_referenceColor, "referencia")
            || !ValidateColor(_backgroundColor, "fondo")) return;
        if (!string.IsNullOrWhiteSpace(_draft.BackgroundImagePath) && !CanReadImage(_draft.BackgroundImagePath))
        {
            _draft.BackgroundImagePath = null; UpdateImageName(); RefreshPreview();
            SetStatus("La imagen ya no está disponible. Se usa el color de fondo; revisa y vuelve a guardar.");
            return;
        }
        _draft = _draft.Normalize();
        if (PersistStyle?.Invoke(EditedStyle) == false)
        {
            SetStatus("No se pudo guardar. Tus cambios siguen aquí; revisa el almacenamiento y vuelve a pulsar Guardar.");
            return;
        }
        DialogResult = true;
    }

    private bool ValidateColor(TextBox field, string label)
    {
        if (BibleVisualStyle.ValidColor(field.Text.Trim())) return true;
        _sections.SelectedIndex = ReferenceEquals(field, _textColor) ? 0 : ReferenceEquals(field, _referenceColor) ? 1 : 2;
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

    private FrameworkElement CreateSlider(string name, double min, double max, double value, double step, Action<double> update, string format)
    {
        var row = new DockPanel();
        var number = new TextBlock { Text = value.ToString(format), MinWidth = 58, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
        var slider = new Slider { Name = name, Minimum = min, Maximum = max, Value = value, TickFrequency = step, SmallChange = step, LargeChange = step * 2, IsSnapToTickEnabled = true };
        slider.Style = (System.Windows.Style)FindResource("EditorSlider");
        slider.Foreground = name == "BibleBackgroundDim" ? Cyan : name == "BibleTransitionDuration" ? Violet : Lime;
        AutomationProperties.SetName(slider, name switch { "BibleFontSize" => "Tamaño de fuente", "BibleBackgroundDim" => "Oscurecer imagen", _ => "Duración de transición" });
        slider.ValueChanged += (_, _) => { number.Text = slider.Value.ToString(format); update(slider.Value); };
        row.Children.Add(slider); return row;
    }

    private static void AddField(Panel parent, string title, FrameworkElement control)
    {
        parent.Children.Add(new TextBlock { Text = title, FontSize = 12, Margin = new Thickness(0, 0, 0, 5) });
        control.Margin = new Thickness(0, 0, 0, 12); AutomationProperties.SetName(control, title); parent.Children.Add(control);
    }

    private Button Action(string title, string name, Action action)
    {
        var button = new Button { Name = name, Content = title, Padding = new Thickness(13, 8, 13, 8), Margin = new Thickness(0, 0, 8, 0) };
        button.Style = (System.Windows.Style)FindResource("EditorButton");
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
