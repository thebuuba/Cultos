using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop.Services;
using Microsoft.Win32;
using static Culto.Desktop.Dialogs.TransitionEditorUi;

namespace Culto.Desktop.Dialogs;

public sealed class SceneTileDialog : Window
{
    private readonly SceneTile _original;
    private readonly TextBox _label, _color;
    private readonly TextBox _programTheme, _programPurpose, _programThemeText, _programPurposeText;
    private readonly StackPanel _programFields;
    private readonly Guid _programCueId;
    private readonly ComboBox _action, _content;
    private readonly ComboBox _transition;
    private readonly Border _preview;
    private readonly Button _browse;
    private readonly TextBlock _error, _contentHint;
    private readonly Cue[] _sources;
    private string? _imageData;
    private Cue? _fileCue;
    private readonly Func<SceneComposition, FrameworkElement>? _scenePreviewFactory;
    public SceneTile? Result { get; private set; }

    public SceneTileDialog(SceneTile? scene, IEnumerable<Cue> sources, IEnumerable<TransitionPreset>? transitions = null,
        Func<SceneComposition, FrameworkElement>? scenePreviewFactory = null)
    {
        _scenePreviewFactory = scenePreviewFactory;
        _original = scene ?? new SceneTile();
        _imageData = _original.ImageData;
        _fileCue = _original.Cue?.Copy();
        _programCueId = _original.Cue?.Id ?? Guid.NewGuid();
        _sources = sources.DistinctBy(c => c.Id).ToArray();
        ConfigureTheme();
        Title = scene is null ? "Añadir escena" : "Editar escena";
        Width = Math.Min(980, SystemParameters.WorkArea.Width - 48);
        Height = Math.Min(800, SystemParameters.WorkArea.Height - 48);
        MinWidth = Math.Min(760, Width); MinHeight = Math.Min(600, Height);
        MaxHeight = SystemParameters.WorkArea.Height - 24;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#191919"); Foreground = Brushes.WhiteSmoke;
        FontFamily = new FontFamily("Segoe UI");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;
        var header = new StackPanel { Margin = new Thickness(28, 24, 28, 0) };
        header.Children.Add(Text(Title, 27, bold: true));
        var description = Text("Elige el contenido y personaliza su acceso en Escenas.", 13, Muted);
        description.Margin = new Thickness(0, 6, 0, 0); header.Children.Add(description);
        root.Children.Add(header);

        var body = new Grid { Margin = new Thickness(28, 22, 28, 24) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);

        var appearance = new StackPanel();
        appearance.Children.Add(Text("Vista previa del botón", 17, bold: true));
        _preview = new Border { Name = "ScenePreview", Width = 144, Height = 144, Padding = new Thickness(12),
            CornerRadius = new CornerRadius(24), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 22) };
        appearance.Children.Add(_preview);
        var artwork = new WrapPanel(); appearance.Children.Add(artwork);
        var chooseArtwork = Button("Cambiar imagen…", "SceneArtwork", ChooseArtwork);
        chooseArtwork.Margin = new Thickness(0, 0, 8, 8); artwork.Children.Add(chooseArtwork);
        var removeArtwork = Button("Quitar", "RemoveSceneArtwork", () => { _imageData = null; UpdatePreview(); });
        removeArtwork.Margin = new Thickness(0, 0, 0, 8); artwork.Children.Add(removeArtwork);
        appearance.Children.Add(Text("La imagen del botón es independiente del contenido que se proyecta.", 12, Muted));
        _color = new TextBox { Name = "SceneColor", Text = _original.Color, MaxLength = 7 };
        DialogUi.Label(appearance, "Color del botón · #RRGGBB", _color); appearance.Children.Add(_color);
        var colors = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) }; appearance.Children.Add(colors);
        foreach (var color in new[] { "#F5FBF3", "#08090C", "#E6F3FC", "#FFF0D7", "#FFFFFF", "#354E6D", "#4274F4", "#BEF264" })
        {
            var swatch = Button("", "", () => _color.Text = color);
            swatch.Width = 36; swatch.Height = 36; swatch.MinHeight = 36; swatch.Padding = new Thickness(0);
            swatch.Background = SceneTileVisual.Background(color); swatch.BorderBrush = Brush("#555555");
            swatch.ToolTip = color; swatch.Margin = new Thickness(0, 0, 12, 10);
            AutomationProperties.SetName(swatch, "Color " + color); colors.Children.Add(swatch);
        }
        var previewCard = Card(appearance, 20); previewCard.VerticalAlignment = VerticalAlignment.Top;
        body.Children.Add(previewCard);

        var settings = new StackPanel(); Grid.SetColumn(settings, 2); body.Children.Add(settings);
        var contentFields = new StackPanel();
        contentFields.Children.Add(Text("Contenido de la escena", 17, bold: true));
        _label = new TextBox { Name = "SceneLabel", Text = _original.Label, MaxLength = 80 };
        DialogUi.Label(contentFields, "Nombre del botón (opcional)", _label); contentFields.Children.Add(_label);
        _action = new ComboBox { Name = "SceneAction", SelectedValuePath = "Tag" };
        foreach (var (id, title) in new[] { ("logo", "Mostrar logo"), ("blackout", "Fondo negro / restaurar"),
            ("image", "Proyectar imagen"), ("video", "Reproducir video"), ("audio", "Reproducir audio"),
            ("powerpoint", "Proyectar PowerPoint"), ("youtube", "YouTube"), ("browser", "Navegador"),
            ("scene", "Escena por capas"), ("program", "Título del programa · Tema y propósito"), ("cue", "Contenido guardado") })
            _action.Items.Add(new ComboBoxItem { Tag = id, Content = title });
        _action.SelectedValue = _original.Action;
        DialogUi.Label(contentFields, "Acción", _action); contentFields.Children.Add(_action);
        _content = new ComboBox { Name = "SceneContent" };
        DialogUi.Label(contentFields, "Contenido", _content); contentFields.Children.Add(_content);
        _browse = Button("Elegir archivo…", "ChooseSceneContent", ChooseContent);
        _browse.HorizontalAlignment = HorizontalAlignment.Stretch; _browse.Margin = new Thickness(0, 12, 0, 10);
        contentFields.Children.Add(_browse);
        _contentHint = Text("", 12, Muted); contentFields.Children.Add(_contentHint);
        _programFields = new StackPanel();
        _programTheme = new TextBox { Name = "ProgramTheme", Text = _original.Cue?.ProgramTitle?.ThemeImagePath ?? "", IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
        _programPurpose = new TextBox { Name = "ProgramPurpose", Text = _original.Cue?.ProgramTitle?.PurposeImagePath ?? "", IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
        _programThemeText = new TextBox { Name = "ProgramThemeText", Text = _original.Cue?.ProgramTitle?.ThemeText ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 76 };
        _programPurposeText = new TextBox { Name = "ProgramPurposeText", Text = _original.Cue?.ProgramTitle?.PurposeText ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 76 };
        DialogUi.Label(_programFields, "1 · Texto del tema (si no usas imagen)", _programThemeText); _programFields.Children.Add(_programThemeText);
        DialogUi.Label(_programFields, "1 · Imagen del tema", _programTheme); _programFields.Children.Add(_programTheme);
        _programFields.Children.Add(Button("Elegir imagen del tema…", "ChooseProgramTheme", () => ChooseProgramImage(_programTheme)));
        _programFields.Children.Add(Button("Usar solo texto del tema", "ClearProgramTheme", () => _programTheme.Clear()));
        DialogUi.Label(_programFields, "2 · Texto del propósito (si no usas imagen)", _programPurposeText); _programFields.Children.Add(_programPurposeText);
        DialogUi.Label(_programFields, "2 · Imagen del propósito", _programPurpose); _programFields.Children.Add(_programPurpose);
        _programFields.Children.Add(Button("Elegir imagen del propósito…", "ChooseProgramPurpose", () => ChooseProgramImage(_programPurpose)));
        _programFields.Children.Add(Button("Usar solo texto del propósito", "ClearProgramPurpose", () => _programPurpose.Clear()));
        contentFields.Children.Add(_programFields);
        settings.Children.Add(Card(contentFields, 20));

        var transitionFields = new StackPanel();
        transitionFields.Children.Add(Text("Transición", 17, bold: true));
        _transition = new ComboBox { Name = "SceneTransitionPreset", SelectedValuePath = "Tag" };
        _transition.Items.Add(new ComboBoxItem { Content = "Usar transición del sistema", Tag = "" });
        foreach (var preset in transitions ?? []) _transition.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset.Id });
        _transition.SelectedValue = _transition.Items.OfType<ComboBoxItem>().Any(i => i.Tag as string == _original.TransitionPresetId) ? _original.TransitionPresetId : "";
        DialogUi.Label(transitionFields, "Al mostrar esta escena", _transition); transitionFields.Children.Add(_transition);
        var transitionCard = Card(transitionFields, 20); transitionCard.Margin = new Thickness(0, 16, 0, 0);
        settings.Children.Add(transitionCard);

        var footer = new Grid { Margin = new Thickness(24, 16, 24, 16) };
        footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _error = Text("", 12, Brush("#F3C779")); _error.Margin = new Thickness(0, 0, 16, 0);
        AutomationProperties.SetLiveSetting(_error, AutomationLiveSetting.Polite);
        footer.Children.Add(_error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var cancel = Button("Cancelar", "CancelScene", () => DialogResult = false); cancel.IsCancel = true;
        cancel.Margin = new Thickness(0, 0, 10, 0); actions.Children.Add(cancel);
        var save = Button("Guardar escena", "SaveScene", Save, true); save.IsDefault = true; actions.Children.Add(save);
        Grid.SetColumn(actions, 1); footer.Children.Add(actions);
        var footerHost = new Border { Background = Brush("#242424"), BorderBrush = Brush("#353535"),
            BorderThickness = new Thickness(0, 1, 0, 0), Child = footer };
        Grid.SetRow(footerHost, 2); root.Children.Add(footerHost);
        _action.SelectionChanged += (_, _) => { _error.Text = ""; _fileCue = null; FillContent(); UpdatePreview(); };
        _content.SelectionChanged += (_, _) => { _error.Text = ""; UpdatePreview(); };
        _label.TextChanged += (_, _) => UpdatePreview(); _color.TextChanged += (_, _) => UpdatePreview();
        foreach (var field in new[] { _programTheme, _programPurpose, _programThemeText, _programPurposeText })
            field.TextChanged += (_, _) => { _error.Text = ""; UpdatePreview(); };
        FillContent(); UpdatePreview();
        Loaded += (_, _) => { _label.Focus(); _label.SelectAll(); };
    }

    private void ConfigureTheme()
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CultoStudio;component/Dialogs/TransitionEditorTheme.xaml", UriKind.Relative) });
        Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CultoStudio;component/Dialogs/SceneTileDialogTheme.xaml", UriKind.Relative) });
        foreach (var type in new[] { typeof(TextBox), typeof(ComboBox) })
        {
            var style = new Style(type, (Style)Application.Current.FindResource(type));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#303030")));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.WhiteSmoke));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#454545")));
            style.Setters.Add(new Setter(Control.MinHeightProperty, 42d));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
            var focus = new Trigger { Property = IsKeyboardFocusWithinProperty, Value = true };
            focus.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#303030")));
            focus.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.WhiteSmoke));
            focus.Setters.Add(new Setter(Control.BorderBrushProperty, Lime));
            style.Triggers.Add(focus);
            if (type == typeof(TextBox))
            {
                style.Setters.Add(new Setter(TextBox.CaretBrushProperty, Lime));
                style.Setters.Add(new Setter(TextBox.SelectionBrushProperty, Lime));
                style.Setters.Add(new Setter(TextBox.SelectionTextBrushProperty, Brush("#191919")));
            }
            else
            {
                style.Setters.Add(new Setter(Control.TemplateProperty, FindResource("SceneEditorComboTemplate")));
                style.Setters.Add(new Setter(ItemsControl.ItemContainerStyleProperty, FindResource("SceneEditorOption")));
            }
            Resources[type] = style;
        }
    }

    private string SelectedAction => _action.SelectedValue as string ?? "image";
    private Cue? SelectedCue => (_content.SelectedItem as ComboBoxItem)?.Tag as Cue;
    private bool Matches(Cue cue) => SelectedAction switch
    {
        "image" => cue.Kind == CueKind.Image, "video" => cue.Kind == CueKind.Video, "audio" => cue.Kind == CueKind.Audio,
        "browser" => cue.Kind == CueKind.BrowserSource, "youtube" => cue.Kind == CueKind.WindowCapture,
        "scene" => cue.Kind == CueKind.Scene, "powerpoint" => cue.Kind == CueKind.PowerPoint,
        "cue" => true, _ => false
    };
    private void FillContent()
    {
        _content.Items.Clear();
        _content.Items.Add(new ComboBoxItem { Content = SelectedAction == "program" ? "Tema → Propósito (dos pantallas)" : SelectedAction == "powerpoint"
            ? "Volver al PowerPoint iniciado en Culto Studio" : "Usar la selección actual del equipo" });
        foreach (var cue in (_fileCue is null ? _sources : new[] { _fileCue }.Concat(_sources)).Where(Matches).DistinctBy(c => c.Id))
            _content.Items.Add(new ComboBoxItem { Content = cue.Title, Tag = cue });
        _content.SelectedItem = _content.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (i.Tag as Cue)?.Id == _fileCue?.Id && i.Tag is not null)
            ?? _content.Items[0];
        _content.IsEnabled = SelectedAction is not ("logo" or "blackout" or "program");
        _programFields.Visibility = SelectedAction == "program" ? Visibility.Visible : Visibility.Collapsed;
        _browse.Visibility = SelectedAction == "program" ? Visibility.Collapsed : Visibility.Visible;
        _browse.IsEnabled = SelectedAction is not ("logo" or "blackout" or "youtube");
        _browse.Content = SelectedAction switch { "scene" => "Editar capas…", "browser" => "Crear navegador…",
            "powerpoint" => "Elegir presentación…", _ => "Elegir archivo…" };
        _contentHint.Text = SelectedAction switch
        {
            "logo" => "Muestra las imágenes elegidas en Configurar logo, desde el menú de la escena.",
            "blackout" => "Muestra el fondo negro; al pulsar de nuevo restaura el contenido.",
            "youtube" => "Elige una ventana de YouTube detectada o usa la selección actual del equipo.",
            "scene" => "Elige una plantilla animada o combina imagen, video, audio, texto, Biblia, navegador y captura en capas independientes.",
            "powerpoint" => "Sin archivo fijo, vuelve a la presentación iniciada en Culto Studio y conserva su diapositiva. También puedes fijar un archivo.",
            "program" => "Escribe el tema y el propósito o elige una imagen para cada pantalla. La imagen tiene prioridad sobre el texto. Siguiente muestra el propósito y Anterior vuelve al tema.",
            _ => "Puedes fijar un archivo o contenido guardado para que esta escena siempre proyecte lo mismo."
        };
    }
    private SceneTile Draft() => _original with { Label = SelectedAction == "program" && string.IsNullOrWhiteSpace(_label.Text) ? "Título del programa" : _label.Text.Trim(), Action = SelectedAction, Color = _color.Text.Trim(),
        ImageData = _imageData, Cue = SelectedAction == "program" ? new Cue { Id = _programCueId, Kind = string.IsNullOrWhiteSpace(_programTheme.Text) ? CueKind.Text : CueKind.Image,
            Title = string.IsNullOrWhiteSpace(_label.Text) ? "Título del programa" : _label.Text.Trim(),
            FilePath = string.IsNullOrWhiteSpace(_programTheme.Text) ? null : _programTheme.Text.Trim(), Body = string.IsNullOrWhiteSpace(_programTheme.Text) ? _programThemeText.Text.Trim() : "",
            ProgramTitle = new(_programTheme.Text.Trim(), _programPurpose.Text.Trim(), _programThemeText.Text.Trim(), _programPurposeText.Text.Trim()) } : SelectedCue?.Copy(),
        TransitionPresetId = _transition.SelectedValue as string is { Length: > 0 } id ? id : null };
    private void UpdatePreview()
    {
        var scene = Draft();
        _preview.Background = SceneTileVisual.Background(scene.Color);
        System.Windows.Documents.TextElement.SetForeground(_preview, SceneTileVisual.Ink(scene.Color));
        _preview.Child = SceneTileVisual.Content(scene);
    }
    private void ChooseArtwork()
    {
        var dialog = new OpenFileDialog { Title = "Imagen del botón", Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dialog.ShowDialog(this) != true) return;
        try { _imageData = SceneArtwork.Import(dialog.FileName); _error.Text = ""; UpdatePreview(); }
        catch (Exception error) { _error.Text = "No se pudo abrir la imagen: " + error.Message; }
    }
    private void ChooseProgramImage(TextBox target)
    {
        var dialog = new OpenFileDialog { Title = target == _programTheme ? "Imagen del tema" : "Imagen del propósito",
            Filter = MediaLibrary.ImageFileFilter, CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        target.Text = dialog.FileName;
        _error.Text = "";
    }
    private void ChooseContent()
    {
        if (SelectedAction is "logo" or "blackout") { _error.Text = "Esta acción no necesita un archivo. Usa Imagen del botón para personalizarla."; return; }
        if (SelectedAction == "scene")
        {
            var editor = new SceneLayersDialog(SelectedCue, _sources, _scenePreviewFactory) { Owner = this };
            if (editor.ShowDialog() != true || editor.SelectedCue is not { } cue) return;
            _fileCue = cue;
        }
        else if (SelectedAction == "browser")
        {
            var editor = new BrowserSourceDialog(SelectedCue) { Owner = this };
            if (editor.ShowDialog() != true || editor.SelectedCue is not { } cue) return;
            _fileCue = cue;
        }
        else
        {
            var dialog = new OpenFileDialog { Title = "Contenido de la escena", Filter = SelectedAction switch
            {
                "image" => MediaLibrary.ImageFileFilter, "video" => MediaLibrary.VideoFileFilter,
                "audio" => MediaLibrary.AudioFileFilter,
                "powerpoint" => "PowerPoint|*.pptx;*.ppt;*.ppsx;*.pps",
                _ => MediaLibrary.FileFilter
            } };
            if (SelectedAction == "youtube") { _error.Text = "Elige una ventana de YouTube detectada en la lista."; return; }
            if (dialog.ShowDialog(this) != true) return;
            try { _fileCue = MediaLibrary.FromFile(dialog.FileName); }
            catch (Exception error) { _error.Text = error.Message; return; }
        }
        if (_label.Text == "Nueva escena") _label.Text = _fileCue.Title;
        FillContent(); UpdatePreview(); _error.Text = "";
    }
    private void Save()
    {
        if (SelectedAction == "program" && (!ProgramPageReady(_programTheme, _programThemeText) || !ProgramPageReady(_programPurpose, _programPurposeText)))
        { _error.Text = "Escribe el tema y el propósito o elige imágenes disponibles para ambas pantallas."; return; }
        if (!System.Text.RegularExpressions.Regex.IsMatch(_color.Text.Trim(), "^#[0-9a-fA-F]{6}$"))
        { _error.Text = "Escribe un color como #24B27E."; return; }
        if (SelectedCue is null && SelectedAction is "cue" or "audio" or "scene")
        { _error.Text = "Elige el contenido que quieres proyectar."; return; }
        if (SelectedCue?.FilePath is { } path && !File.Exists(path)) { _error.Text = "No se encuentra el archivo de esta escena."; return; }
        Result = Draft(); DialogResult = true;
    }

    private static bool ProgramPageReady(TextBox image, TextBox text) => string.IsNullOrWhiteSpace(image.Text)
        ? !string.IsNullOrWhiteSpace(text.Text) : File.Exists(image.Text.Trim());
}
