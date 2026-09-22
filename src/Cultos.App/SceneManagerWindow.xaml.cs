using Cultos.Core;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Cultos.App;

public partial class SceneManagerWindow : Window
{
    private readonly LocalDatabase _db;
    private readonly List<SceneTypeOption> _types =
    [
        new(SceneType.Custom, "Personalizada"),
        new(SceneType.Logo, "Logo / carrusel"),
        new(SceneType.Bible, "Biblia"),
        new(SceneType.Hymn, "Himno"),
        new(SceneType.Song, "Canción"),
        new(SceneType.Video, "Video"),
        new(SceneType.YouTube, "YouTube"),
        new(SceneType.Title, "Tema / texto"),
        new(SceneType.Black, "Fondo oscuro"),
        new(SceneType.Image, "Imagen"),
        new(SceneType.Audio, "Audio")
    ];

    private List<PresentationScene> _scenes = [];
    private PresentationScene? _selected;
    private bool _creatingNew;

    public SceneManagerWindow(LocalDatabase db)
    {
        InitializeComponent();
        _db = db;
        TypeBox.ItemsSource = _types;
        Reload();
    }

    private void Reload(int? selectedId = null)
    {
        _scenes = _db.ListScenes();
        SceneListBox.ItemsSource = null;
        SceneListBox.ItemsSource = _scenes;

        if (selectedId is int id)
            SceneListBox.SelectedItem = _scenes.FirstOrDefault(x => x.Id == id);

        if (SceneListBox.SelectedItem is null && _scenes.Count > 0)
            SceneListBox.SelectedIndex = 0;
    }

    private void SceneListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SceneListBox.SelectedItem is not PresentationScene scene) return;
        _creatingNew = false;
        _selected = scene;
        LoadEditor(scene);
    }

    private void LoadEditor(PresentationScene scene)
    {
        EditorTitle.Text = scene.Name;
        NameBox.Text = scene.Name;
        TypeBox.SelectedValue = scene.Type;
        TypeBox.IsEnabled = !scene.IsBuiltIn;

        var content = !string.IsNullOrWhiteSpace(scene.Title)
            ? scene.Title
            : !string.IsNullOrWhiteSpace(scene.MediaPath)
                ? Path.GetFileName(scene.MediaPath)
                : !string.IsNullOrWhiteSpace(scene.Content)
                    ? "Contenido preparado"
                    : "Sin contenido preparado";
        ContentStateText.Text = content;

        BuiltInText.Text = scene.IsBuiltIn
            ? "Escena predeterminada del sistema"
            : "Escena personalizada";

        LogoOptionsPanel.Visibility = scene.Type == SceneType.Logo
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (scene.Type == SceneType.Logo)
        {
            var settings = ReadLogoSettings(scene);
            LogoIntervalBox.Text = settings.IntervalSeconds.ToString();
            LogoLoopBox.IsChecked = settings.Loop;
            ContentStateText.Text = settings.ImagePaths.Count == 0
                ? "Sin imágenes asignadas"
                : $"{settings.ImagePaths.Count} imágenes asignadas";
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _creatingNew = true;
        _selected = new PresentationScene
        {
            Name = "Nueva escena",
            Type = SceneType.Custom,
            Position = _scenes.Count == 0 ? 0 : _scenes.Max(x => x.Position) + 1,
            IsBuiltIn = false
        };

        SceneListBox.SelectedItem = null;
        EditorTitle.Text = "Nueva escena";
        NameBox.Text = "Nueva escena";
        TypeBox.SelectedValue = SceneType.Custom;
        TypeBox.IsEnabled = true;
        ContentStateText.Text = "Sin contenido preparado";
        BuiltInText.Text = "Escena personalizada";
        LogoOptionsPanel.Visibility = Visibility.Collapsed;
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;

        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Escribe un nombre para la escena.", "Escenas", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _selected.Name = name;

        if (!_selected.IsBuiltIn && TypeBox.SelectedValue is SceneType type)
            _selected.Type = type;

        if (_selected.Type == SceneType.Logo)
        {
            var settings = ReadLogoSettings(_selected);
            settings.IntervalSeconds = int.TryParse(LogoIntervalBox.Text, out var seconds)
                ? Math.Clamp(seconds, 2, 120)
                : 8;
            settings.Loop = LogoLoopBox.IsChecked != false;
            _selected.SettingsJson = JsonSerializer.Serialize(settings);
        }

        _db.SaveScene(_selected);
        var id = _selected.Id;
        _creatingNew = false;
        Reload(id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SceneListBox.SelectedItem is not PresentationScene scene) return;

        if (scene.IsBuiltIn)
        {
            MessageBox.Show(
                "Las escenas predeterminadas no se eliminan porque forman parte del flujo base de Cultos. Puedes renombrarlas y reordenarlas.",
                "Escenas",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(
                $"¿Eliminar la escena “{scene.Name}”?",
                "Eliminar escena",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _db.DeleteScene(scene.Id);
        Reload();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        if (SceneListBox.SelectedItem is not PresentationScene scene) return;
        var index = _scenes.FindIndex(x => x.Id == scene.Id);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _scenes.Count) return;

        (_scenes[index], _scenes[target]) = (_scenes[target], _scenes[index]);

        for (var i = 0; i < _scenes.Count; i++)
        {
            _scenes[i].Position = i;
            _db.SaveScene(_scenes[i]);
        }

        Reload(scene.Id);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static LogoSceneSettings ReadLogoSettings(PresentationScene scene)
    {
        try
        {
            return JsonSerializer.Deserialize<LogoSceneSettings>(scene.SettingsJson) ?? new LogoSceneSettings();
        }
        catch
        {
            return new LogoSceneSettings();
        }
    }
}

public sealed record SceneTypeOption(SceneType Value, string Label);
