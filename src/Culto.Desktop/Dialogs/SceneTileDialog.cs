using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Culto.Core;
using Culto.Desktop.Services;
using Microsoft.Win32;

namespace Culto.Desktop.Dialogs;

public sealed class SceneTileDialog : Window
{
    private readonly SceneTile _original;
    private readonly TextBox _label, _color;
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
        _sources = sources.DistinctBy(c => c.Id).ToArray();
        var panel = DialogUi.Panel(this, scene is null ? "Añadir escena" : "Editar escena",
            "Elige qué hace este botón y personaliza su nombre, color e imagen.");
        Width = 560;
        _preview = new Border { Width = 100, Height = 100, Padding = new Thickness(6), CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(_preview);
        _label = new TextBox { Text = _original.Label, MaxLength = 80 };
        DialogUi.Label(panel, "Nombre del botón (opcional)", _label); panel.Children.Add(_label);
        _action = new ComboBox { SelectedValuePath = "Tag" };
        foreach (var (id, title) in new[] { ("logo", "Mostrar logo"), ("blackout", "Fondo negro / restaurar"),
            ("image", "Proyectar imagen"), ("video", "Reproducir video"), ("audio", "Reproducir audio"),
            ("youtube", "YouTube"), ("browser", "Navegador"), ("scene", "Escena por capas"), ("cue", "Contenido guardado") })
            _action.Items.Add(new ComboBoxItem { Tag = id, Content = title });
        _action.SelectedValue = _original.Action;
        DialogUi.Label(panel, "Acción", _action); panel.Children.Add(_action);
        _content = new ComboBox(); DialogUi.Label(panel, "Contenido de esta escena", _content); panel.Children.Add(_content);
        _browse = DialogUi.Button("Elegir archivo…", ChooseContent); panel.Children.Add(_browse);
        _contentHint = new TextBlock { TextWrapping = TextWrapping.Wrap, Style = (Style)FindResource("MutedText"), FontSize = 12 };
        panel.Children.Add(_contentHint);
        var artwork = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) }; panel.Children.Add(artwork);
        artwork.Children.Add(DialogUi.Button("Imagen del botón…", ChooseArtwork));
        artwork.Children.Add(DialogUi.Button("Quitar imagen", () => { _imageData = null; UpdatePreview(); }));
        panel.Children.Add(new TextBlock { Text = "La imagen del botón no cambia el contenido que se proyecta.", FontSize = 12,
            TextWrapping = TextWrapping.Wrap, Style = (Style)FindResource("MutedText") });
        _color = new TextBox { Text = _original.Color, MaxLength = 7 };
        _transition = new ComboBox { Name = "SceneTransitionPreset", SelectedValuePath = "Tag" };
        _transition.Items.Add(new ComboBoxItem { Content = "Usar transición del sistema", Tag = "" });
        foreach (var preset in transitions ?? []) _transition.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset.Id });
        _transition.SelectedValue = _transition.Items.OfType<ComboBoxItem>().Any(i => i.Tag as string == _original.TransitionPresetId) ? _original.TransitionPresetId : "";
        DialogUi.Label(panel, "Transición al mostrar esta escena", _transition); panel.Children.Add(_transition);
        DialogUi.Label(panel, "Color de fondo (#RRGGBB)", _color); panel.Children.Add(_color);
        var colors = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) }; panel.Children.Add(colors);
        foreach (var color in new[] { "#F5FBF3", "#08090C", "#E6F3FC", "#FFF0D7", "#FFFFFF", "#354E6D", "#4274F4", "#24B27E" })
        {
            var swatch = new Button { Width = 36, Height = 32, MinWidth = 32, MinHeight = 32, Padding = new Thickness(0),
                Background = SceneTileVisual.Background(color), ToolTip = color, Margin = new Thickness(0, 0, 6, 0) };
            AutomationProperties.SetName(swatch, "Color " + color);
            swatch.Click += (_, _) => _color.Text = color; colors.Children.Add(swatch);
        }
        _error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("AmberBrush"), Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(_error);
        var actions = DialogUi.Actions(panel);
        var cancel = DialogUi.Button("Cancelar", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        var save = DialogUi.Button("Guardar escena", Save, true); save.IsDefault = true; actions.Children.Add(save);
        _action.SelectionChanged += (_, _) => { _fileCue = null; FillContent(); UpdatePreview(); };
        _content.SelectionChanged += (_, _) => UpdatePreview();
        _label.TextChanged += (_, _) => UpdatePreview(); _color.TextChanged += (_, _) => UpdatePreview();
        FillContent(); UpdatePreview();
        Loaded += (_, _) => { _label.Focus(); _label.SelectAll(); };
    }

    private string SelectedAction => _action.SelectedValue as string ?? "image";
    private Cue? SelectedCue => (_content.SelectedItem as ComboBoxItem)?.Tag as Cue;
    private bool Matches(Cue cue) => SelectedAction switch
    {
        "image" => cue.Kind == CueKind.Image, "video" => cue.Kind == CueKind.Video, "audio" => cue.Kind == CueKind.Audio,
        "browser" => cue.Kind == CueKind.BrowserSource, "youtube" => cue.Kind == CueKind.WindowCapture,
        "scene" => cue.Kind == CueKind.Scene,
        "cue" => true, _ => false
    };
    private void FillContent()
    {
        _content.Items.Clear();
        _content.Items.Add(new ComboBoxItem { Content = "Usar la selección actual del equipo" });
        foreach (var cue in (_fileCue is null ? _sources : new[] { _fileCue }.Concat(_sources)).Where(Matches).DistinctBy(c => c.Id))
            _content.Items.Add(new ComboBoxItem { Content = cue.Title, Tag = cue });
        _content.SelectedItem = _content.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (i.Tag as Cue)?.Id == _fileCue?.Id && i.Tag is not null)
            ?? _content.Items[0];
        _content.IsEnabled = SelectedAction is not ("logo" or "blackout");
        _browse.IsEnabled = SelectedAction is not ("logo" or "blackout" or "youtube");
        _browse.Content = SelectedAction switch { "scene" => "Editar capas…", "browser" => "Crear navegador…", _ => "Elegir archivo…" };
        _contentHint.Text = SelectedAction switch
        {
            "logo" => "Muestra las imágenes elegidas en Configurar logo, desde el menú de la escena.",
            "blackout" => "Muestra el fondo negro; al pulsar de nuevo restaura el contenido.",
            "youtube" => "Elige una ventana de YouTube detectada o usa la selección actual del equipo.",
            "scene" => "Combina imagen, video, audio, texto, Biblia, navegador y captura en capas independientes.",
            _ => "Puedes fijar un archivo o contenido guardado para que esta escena siempre proyecte lo mismo."
        };
    }
    private SceneTile Draft() => _original with { Label = _label.Text.Trim(), Action = SelectedAction, Color = _color.Text.Trim(),
        ImageData = _imageData, Cue = SelectedCue?.Copy(), TransitionPresetId = _transition.SelectedValue as string is { Length: > 0 } id ? id : null };
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
                "image" => "Imágenes|*.png;*.jpg;*.jpeg;*.bmp", "video" => "Videos|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.wmv",
                "audio" => "Audio|*.mp3;*.wav;*.m4a;*.flac;*.ogg;*.wma", _ => "Contenido|*.png;*.jpg;*.jpeg;*.bmp;*.mp4;*.mkv;*.mov;*.webm;*.mp3;*.wav;*.m4a;*.pptx;*.ppt"
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
        if (!System.Text.RegularExpressions.Regex.IsMatch(_color.Text.Trim(), "^#[0-9a-fA-F]{6}$"))
        { _error.Text = "Escribe un color como #24B27E."; return; }
        if (SelectedCue is null && SelectedAction is "cue" or "audio" or "scene")
        { _error.Text = "Elige el contenido que quieres proyectar."; return; }
        if (SelectedCue?.FilePath is { } path && !File.Exists(path)) { _error.Text = "No se encuentra el archivo de esta escena."; return; }
        Result = Draft(); DialogResult = true;
    }
}
