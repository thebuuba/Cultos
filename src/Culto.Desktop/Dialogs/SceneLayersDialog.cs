using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Services;
using Microsoft.Win32;

namespace Culto.Desktop.Dialogs;

public sealed class SceneLayersDialog : Window
{
    private readonly Cue _original;
    private readonly SceneComposition _scene;
    private readonly ListBox _layers = new() { Height = 160, DisplayMemberPath = "Name" };
    private readonly ComboBox _sources = new() { DisplayMemberPath = "Title", MinWidth = 210 };
    private readonly TextBox _title, _name, _background, _canvasWidth, _canvasHeight;
    private readonly Dictionary<string, TextBox> _numbers = [];
    private readonly CheckBox _visible = new() { Content = "Mostrar capa" }, _muted = new() { Content = "Silenciar capa" };
    private readonly Border _preview = new() { Height = 240, Background = Brushes.Black, ClipToBounds = true };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _properties = new();
    private bool _loading;
    private SceneLayer? _selected;
    private Func<SceneComposition, FrameworkElement>? _previewFactory;
    public Cue? SelectedCue { get; private set; }
    public Func<SceneComposition, FrameworkElement>? PreviewFactory
    {
        get => _previewFactory;
        set { _previewFactory = value; RefreshPreview(); }
    }

    public SceneLayersDialog(Cue? scene, IEnumerable<Cue> sources, Func<SceneComposition, FrameworkElement>? previewFactory = null)
    {
        _original = scene?.Copy() ?? new Cue { Kind = CueKind.Scene, Title = "Escena por capas" };
        _scene = (_original.Scene ?? new SceneComposition()).Normalize();
        _previewFactory = previewFactory;
        var panel = DialogUi.Panel(this, "Editar capas", "Las capas se dibujan de abajo hacia arriba. Reutiliza una fuente guardada para conservar su identidad entre escenas.");
        Width = 880;
        _title = Field(panel, "Nombre de la escena", _original.Title);
        panel.Children.Add(_preview);
        var canvasOptions = new UniformGrid { Columns = 3 };
        panel.Children.Add(canvasOptions);
        _canvasWidth = GroupField(canvasOptions, "Ancho del lienzo", _scene.Width.ToString());
        _canvasHeight = GroupField(canvasOptions, "Alto del lienzo", _scene.Height.ToString());
        _background = GroupField(canvasOptions, "Fondo (#RRGGBB)", _scene.Background);
        foreach (var box in new[] { _canvasWidth, _canvasHeight, _background }) box.LostFocus += (_, _) => ApplyAndPreview();
        DialogUi.Label(panel, "Capas (la última queda delante)", _layers);
        panel.Children.Add(_layers);
        var layerActions = new WrapPanel(); panel.Children.Add(layerActions);
        AddButton(layerActions, "Eliminar", () => { if (_selected is { } layer) { _scene.Layers.Remove(layer); RefreshLayers(); } });
        AddButton(layerActions, "Hacia atrás ↓", () => Reorder(-1));
        AddButton(layerActions, "Hacia delante ↑", () => Reorder(1));
        DialogUi.Label(panel, "Añadir fuente guardada", _sources);
        foreach (var source in sources.Where(c => c.Kind is not (CueKind.Scene or CueKind.PowerPoint)).DistinctBy(c => c.Id))
            _sources.Items.Add(source);
        if (_sources.Items.Count > 0) _sources.SelectedIndex = 0;
        panel.Children.Add(_sources);
        var addActions = new WrapPanel(); panel.Children.Add(addActions);
        AddButton(addActions, "Añadir seleccionada", () => { if (_sources.SelectedItem is Cue source) AddLayer(source, source.Id.ToString("N")); });
        AddButton(addActions, "Elegir archivo…", ChooseFile);
        AddButton(addActions, "Añadir texto…", AddText);
        AddButton(addActions, "Añadir navegador…", AddBrowser);
        panel.Children.Add(_properties);
        _name = Field(_properties, "Nombre de la capa", "");
        var toggles = new WrapPanel(); toggles.Children.Add(_visible); toggles.Children.Add(_muted); _properties.Children.Add(toggles);
        _muted.Margin = new Thickness(18, 0, 0, 0);
        var transforms = new UniformGrid { Columns = 4 }; _properties.Children.Add(transforms);
        foreach (var label in new[] { "X", "Y", "Ancho", "Alto", "Rotación (°)", "Opacidad (0–1)", "Volumen (0–100)" })
            _numbers[label] = GroupField(transforms, label, "0");
        _name.LostFocus += (_, _) => ApplyAndPreview();
        foreach (var box in _numbers.Values) box.LostFocus += (_, _) => ApplyAndPreview();
        _visible.Click += (_, _) => ApplyAndPreview(); _muted.Click += (_, _) => ApplyAndPreview();
        _layers.SelectionChanged += (_, _) => LoadSelection();
        _error.Foreground = (Brush)FindResource("AmberBrush"); panel.Children.Add(_error);
        var actions = DialogUi.Actions(panel);
        var cancel = DialogUi.Button("Cancelar", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        actions.Children.Add(DialogUi.Button("Guardar capas", Save, true));
        Closed += (_, _) => DisposePreview();
        RefreshLayers();
    }

    private static TextBox Field(Panel panel, string label, string value)
    {
        var box = new TextBox { Text = value };
        DialogUi.Label(panel, label, box); panel.Children.Add(box); return box;
    }
    private static TextBox GroupField(Panel panel, string label, string value)
    {
        var group = new StackPanel { Margin = new Thickness(0, 0, 10, 0) }; panel.Children.Add(group);
        return Field(group, label, value);
    }
    private static void AddButton(Panel panel, string label, Action action)
    {
        var button = DialogUi.Button(label, action); button.Margin = new Thickness(0, 6, 8, 6); panel.Children.Add(button);
    }
    private void RefreshLayers(SceneLayer? selected = null)
    {
        _layers.ItemsSource = null; _layers.ItemsSource = _scene.Layers;
        _layers.SelectedItem = selected ?? _scene.Layers.LastOrDefault();
        LoadSelection(); RefreshPreview();
    }
    private void LoadSelection()
    {
        if (_loading) return;
        _loading = true;
        _selected = _layers.SelectedItem as SceneLayer;
        _properties.IsEnabled = _selected is not null;
        if (_selected is { } layer)
        {
            _name.Text = layer.Name; _visible.IsChecked = layer.Visible; _muted.IsChecked = layer.Muted;
            var values = new[] { layer.X, layer.Y, layer.Width, layer.Height, layer.Rotation, layer.Opacity, layer.Volume };
            foreach (var pair in _numbers.Values.Zip(values)) pair.First.Text = pair.Second.ToString(CultureInfo.InvariantCulture);
        }
        _loading = false;
    }
    private double Read(string field)
    {
        var text = _numbers[field].Text.Trim();
        if (!(double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) || !double.IsFinite(value))
            throw new ArgumentException(field + ": escribe un número válido.");
        return value;
    }
    private void ApplyFields()
    {
        if (!int.TryParse(_canvasWidth.Text, out var width) || !int.TryParse(_canvasHeight.Text, out var height))
            throw new ArgumentException("Escribe dimensiones enteras para el lienzo.");
        // Apply atomically: incomplete input must not poison the working scene.
        var draft = _scene.Copy();
        draft.Width = width; draft.Height = height; draft.Background = _background.Text.Trim();
        if (_selected is { } layer)
        {
            var edited = draft.Layers[_scene.Layers.IndexOf(layer)];
            edited.Name = _name.Text.Trim(); edited.Visible = _visible.IsChecked == true; edited.Muted = _muted.IsChecked == true;
            edited.X = Read("X"); edited.Y = Read("Y"); edited.Width = Read("Ancho"); edited.Height = Read("Alto");
            edited.Rotation = Read("Rotación (°)"); edited.Opacity = Read("Opacidad (0–1)");
            var volume = Read("Volumen (0–100)");
            if (volume != Math.Truncate(volume) || volume is < 0 or > 100) throw new ArgumentException("El volumen debe ser un entero entre 0 y 100.");
            edited.Volume = (int)volume;
        }
        draft.Validate();
        _scene.Width = draft.Width; _scene.Height = draft.Height; _scene.Background = draft.Background;
        if (_selected is { } current)
        {
            var index = _scene.Layers.IndexOf(current);
            _scene.Layers[index] = draft.Layers[index]; _selected = _scene.Layers[index];
            _loading = true; _layers.Items.Refresh(); _layers.SelectedItem = _selected; _loading = false;
        }
    }
    private void ApplyAndPreview()
    {
        if (_loading) return;
        TryAction(() => { ApplyFields(); RefreshPreview(); });
    }
    private void Reorder(int direction) => TryAction(() =>
    {
        ApplyFields();
        if (_selected is not { } layer) return;
        var index = _scene.Layers.IndexOf(layer); var target = index + direction;
        if (target < 0 || target >= _scene.Layers.Count) return;
        _scene.Layers.RemoveAt(index); _scene.Layers.Insert(target, layer); RefreshLayers(layer);
    });
    private void AddLayer(Cue source, string? sourceId = null) => TryAction(() =>
    {
        ApplyFields();
        if (_scene.Layers.Count >= SceneComposition.MaxLayers) throw new ArgumentException("La escena admite hasta 32 capas.");
        var layer = new SceneLayer { Source = source.Copy(), Name = source.Title, Width = _scene.Width, Height = _scene.Height };
        if (sourceId is not null) layer.SourceId = sourceId;
        _scene.Layers.Add(layer); RefreshLayers(layer);
    });
    private void ChooseFile()
    {
        var dialog = new OpenFileDialog { Title = "Añadir fuente", Filter = MediaLibrary.MediaFileFilter };
        if (dialog.ShowDialog(this) == true) TryAction(() => AddLayer(MediaLibrary.FromFile(dialog.FileName)));
    }
    private void AddText()
    {
        var editor = new CueEditDialog(new Cue { Kind = CueKind.Text, Title = "Texto" }) { Owner = this };
        if (editor.ShowDialog() == true) AddLayer(new Cue { Kind = CueKind.Text, Title = editor.CueTitle, Body = editor.Body });
    }
    private void AddBrowser()
    {
        var editor = new BrowserSourceDialog { Owner = this };
        if (editor.ShowDialog() == true && editor.SelectedCue is { } source) AddLayer(source);
    }
    private void Save() => TryAction(() =>
    {
        ApplyFields();
        if (string.IsNullOrWhiteSpace(_title.Text)) throw new ArgumentException("Escribe un nombre para la escena.");
        var cue = _original.Copy(); cue.Kind = CueKind.Scene; cue.Title = _title.Text.Trim(); cue.Scene = _scene.Normalize();
        SelectedCue = cue; DialogResult = true;
    });
    private void TryAction(Action action)
    {
        try { _error.Text = ""; action(); }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException)
        { _error.Text = error.Message; }
    }
    private void DisposePreview()
    {
        if (_preview.Child is IDisposable disposable) disposable.Dispose();
        _preview.Child = null;
    }
    private void RefreshPreview()
    {
        DisposePreview();
        if (_previewFactory is not null) { _preview.Child = _previewFactory(_scene.Copy()); return; }
        var canvas = new Canvas { Width = _scene.Width, Height = _scene.Height, Background = (Brush)new BrushConverter().ConvertFromString(_scene.Background)!, ClipToBounds = true };
        foreach (var layer in _scene.Layers.Where(l => l.Visible))
        {
            var visual = new Border { Width = layer.Width, Height = layer.Height, Opacity = layer.Opacity,
                BorderBrush = Brushes.LightSkyBlue, BorderThickness = new Thickness(4), Background = new SolidColorBrush(Color.FromArgb(100, 35, 65, 95)),
                RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(layer.Rotation) };
            if (layer.Source.Kind == CueKind.Image && layer.Source.FilePath is { } path && File.Exists(path))
            {
                try { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(Path.GetFullPath(path)); bitmap.EndInit(); bitmap.Freeze(); visual.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform }; }
                catch (Exception error) when (error is IOException or NotSupportedException or ArgumentException) { visual.Child = Caption(layer); }
            }
            else visual.Child = Caption(layer);
            Canvas.SetLeft(visual, layer.X); Canvas.SetTop(visual, layer.Y); canvas.Children.Add(visual);
        }
        _preview.Child = new Viewbox { Stretch = Stretch.Uniform, Child = canvas };
    }
    private static TextBlock Caption(SceneLayer layer) => new() { Text = layer.Source.Kind is CueKind.Text or CueKind.Bible ? layer.Source.Body : layer.Name + "\n" + layer.Source.Kind,
        Foreground = Brushes.White, FontSize = 48, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12) };
}
