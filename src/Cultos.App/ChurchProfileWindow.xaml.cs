using Cultos.Core;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;

namespace Cultos.App;

public partial class ChurchProfileWindow : Window
{
    private readonly ChurchProfileStore _store;
    private readonly LocalDatabase _db;
    private ChurchProfile _profile;
    private string? _pendingLogoPath;
    private string? _pendingBackgroundPath;

    public bool ProfileChanged { get; private set; }

    public ChurchProfileWindow(ChurchProfileStore store, LocalDatabase db)
    {
        InitializeComponent();
        _store = store;
        _db = db;
        _profile = _store.Load();
        LoadProfileIntoEditor();
    }

    private void LoadProfileIntoEditor()
    {
        NameBox.Text = _profile.Name;
        ShortNameBox.Text = _profile.ShortName;
        LocationBox.Text = _profile.Location;
        BibleTranslationBox.Text = _profile.PreferredBibleTranslation;
        HymnalBox.Text = _profile.PreferredHymnal;
        AccentBox.Text = _profile.AccentHex;
        _pendingLogoPath = null;
        _pendingBackgroundPath = null;
        UpdateLogoPreview(_profile.LogoPath);
        BackgroundStateText.Text = !string.IsNullOrWhiteSpace(_profile.DefaultBackgroundPath) &&
                                   File.Exists(_profile.DefaultBackgroundPath)
            ? Path.GetFileName(_profile.DefaultBackgroundPath)
            : "Sin fondo predeterminado";
    }

    private void ChooseLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.bmp|Todos los archivos|*.*",
            Title = "Elegir logo de la iglesia"
        };
        if (dialog.ShowDialog() != true) return;

        _pendingLogoPath = dialog.FileName;
        UpdateLogoPreview(_pendingLogoPath);
    }

    private void ChooseBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.bmp|Todos los archivos|*.*",
            Title = "Elegir fondo predeterminado"
        };
        if (dialog.ShowDialog() != true) return;

        _pendingBackgroundPath = dialog.FileName;
        BackgroundStateText.Text = Path.GetFileName(dialog.FileName);
    }

    private void UpdateLogoPreview(string? path)
    {
        LogoPreview.Source = null;
        LogoPlaceholder.Visibility = Visibility.Visible;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            LogoPreview.Source = image;
            LogoPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            LogoPlaceholder.Text = "No se pudo mostrar el logo";
        }
    }

    private bool SaveProfile()
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Escribe el nombre de la iglesia.", "Perfil de iglesia", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        var accent = AccentBox.Text.Trim();
        try
        {
            _ = (Color)ColorConverter.ConvertFromString(accent);
        }
        catch
        {
            MessageBox.Show("El color de acento no es válido. Usa un valor como #C9AC70.", "Perfil de iglesia", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_pendingLogoPath))
            _profile.LogoPath = _store.ImportAsset(_pendingLogoPath, "logo");

        if (!string.IsNullOrWhiteSpace(_pendingBackgroundPath))
            _profile.DefaultBackgroundPath = _store.ImportAsset(_pendingBackgroundPath, "fondo");

        _profile.Name = name;
        _profile.ShortName = ShortNameBox.Text.Trim();
        _profile.Location = LocationBox.Text.Trim();
        _profile.PreferredBibleTranslation = BibleTranslationBox.Text.Trim();
        _profile.PreferredHymnal = HymnalBox.Text.Trim();
        _profile.AccentHex = accent;
        _store.Save(_profile);

        _pendingLogoPath = null;
        _pendingBackgroundPath = null;
        ProfileChanged = true;
        return true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveProfile()) return;
        LoadProfileIntoEditor();
        MessageBox.Show("Perfil de iglesia guardado.", "Cultos", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveProfile()) return;

        var safeName = string.Concat(_profile.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var dialog = new SaveFileDialog
        {
            Filter = "Perfil de Cultos|*.cultosperfil",
            FileName = string.IsNullOrWhiteSpace(safeName) ? "perfil-iglesia.cultosperfil" : safeName + ".cultosperfil",
            Title = "Exportar perfil de iglesia"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            _store.ExportPackage(dialog.FileName, _profile, _db.ListScenes());
            MessageBox.Show(
                "Perfil exportado correctamente. El paquete incluye escenas y recursos asociados.",
                "Cultos",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo exportar el perfil de iglesia", ex);
            MessageBox.Show("No se pudo exportar el perfil. " + ex.Message, "Cultos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Perfil de Cultos|*.cultosperfil",
            Title = "Importar perfil de iglesia"
        };
        if (dialog.ShowDialog() != true) return;

        if (MessageBox.Show(
                "Importar este perfil reemplazará la configuración de escenas personalizadas actual. ¿Continuar?",
                "Importar perfil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            var result = _store.ImportPackage(dialog.FileName);
            _db.ImportSceneProfile(result.Scenes);
            _store.Save(result.Profile);
            _profile = result.Profile;
            ProfileChanged = true;
            LoadProfileIntoEditor();

            MessageBox.Show(
                "Perfil importado correctamente. Las escenas y recursos quedaron instalados en esta computadora.",
                "Cultos",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo importar el perfil de iglesia", ex);
            MessageBox.Show("No se pudo importar el perfil. " + ex.Message, "Cultos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
