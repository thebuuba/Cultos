using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Culto.Desktop.Dialogs;

public sealed class BackgroundGalleryDialog : Window
{
    private readonly WrapPanel _gallery = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBlock _empty = new() { Text = "Sin imágenes", Margin = new Thickness(4, 24, 4, 0) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly Dictionary<string, BitmapSource?> _thumbnails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Style _tileStyle;

    public List<string> Images { get; }
    public string? SelectedPath { get; private set; }
    public bool RestoreContent { get; private set; }

    public BackgroundGalleryDialog(IReadOnlyList<string> images, string? selectedPath, bool canRestore)
    {
        Images = images.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        SelectedPath = selectedPath;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Fondo de la iglesia";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        MaxWidth = Math.Max(1, SystemParameters.WorkArea.Width - 32);
        MaxHeight = Math.Max(1, SystemParameters.WorkArea.Height - 32);
        Width = Math.Min(600, MaxWidth); Height = Math.Min(500, MaxHeight);
        MinWidth = Math.Min(360, MaxWidth); MinHeight = Math.Min(320, MaxHeight);
        _tileStyle = CreateTileStyle();

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        heading.Children.Add(new TextBlock { Text = Title, FontSize = 24, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        var add = new Button
        {
            Name = "AddImagesButton", Content = "+ Agregar imágenes", HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 14, 0, 0), Style = (Style)FindResource("PrimaryButton")
        };
        add.Click += (_, _) => AddImages();
        AutomationProperties.SetName(add, "Agregar imágenes de fondo");
        heading.Children.Add(add); root.Children.Add(heading);

        var galleryArea = new Grid();
        galleryArea.Children.Add(new ScrollViewer
        {
            Content = _gallery, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false
        });
        _empty.Foreground = (Brush)FindResource("MutedBrush");
        _empty.IsHitTestVisible = false; galleryArea.Children.Add(_empty);
        Grid.SetRow(galleryArea, 1); root.Children.Add(galleryArea);

        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        _status.Foreground = (Brush)FindResource("AmberBrush");
        _status.FontSize = 12; _status.Margin = new Thickness(0, 0, 0, 8);
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        footer.Children.Add(_status);
        var actions = new WrapPanel();
        var neutral = ActionButton("Sin imagen", () => { SelectedPath = null; DialogResult = true; });
        neutral.Name = "NoImageButton"; actions.Children.Add(neutral);
        if (canRestore)
        {
            var restore = ActionButton("Volver al contenido", () => { RestoreContent = true; DialogResult = true; });
            restore.Name = "RestoreContentButton"; actions.Children.Add(restore);
        }
        var close = ActionButton("Cerrar", () => DialogResult = false);
        close.Name = "CloseGalleryButton"; close.IsCancel = true; actions.Children.Add(close);
        footer.Children.Add(actions); Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = root;
        RenderGallery();
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 8, 12, 8) };
        button.Click += (_, _) => action(); return button;
    }

    private Style CreateTileStyle()
    {
        // Keep gallery cards rounded while ordinary actions retain the shared capsule style.
        var style = new Style(typeof(Button), (Style)FindResource(typeof(Button)));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(MarginProperty, new Thickness(8));
        border.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        template.Triggers.Add(new Trigger { Property = IsMouseOverProperty, Value = true, Setters = { new Setter(BorderBrushProperty, FindResource("AccentBrush")) } });
        template.Triggers.Add(new Trigger { Property = IsKeyboardFocusedProperty, Value = true, Setters = { new Setter(BorderBrushProperty, FindResource("AmberBrush")) } });
        template.Triggers.Add(new Trigger { Property = Button.IsPressedProperty, Value = true, Setters = { new Setter(OpacityProperty, .75d) } });
        template.Triggers.Add(new Trigger { Property = IsEnabledProperty, Value = false, Setters = { new Setter(OpacityProperty, .45d) } });
        style.Setters.Add(new Setter(TemplateProperty, template));
        style.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));
        return style;
    }

    private void RenderGallery()
    {
        _gallery.Children.Clear(); _empty.Visibility = Images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var path in Images)
        {
            if (!_thumbnails.TryGetValue(path, out var thumbnail)) _thumbnails[path] = thumbnail = ReadThumbnail(path);
            var selected = string.Equals(path, SelectedPath, StringComparison.OrdinalIgnoreCase);
            var title = ImageTitle(path);
            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var picture = new Border { Background = (Brush)FindResource("InputBrush"), CornerRadius = new CornerRadius(6) };
            picture.Child = thumbnail is not null
                ? new Image { Source = thumbnail, Stretch = Stretch.Uniform }
                : new TextBlock { Text = "No disponible", FontSize = 11, Foreground = (Brush)FindResource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(picture);
            var caption = new DockPanel { Margin = new Thickness(0, 7, 0, 0), LastChildFill = true };
            if (selected)
            {
                var indicator = new TextBlock { Text = "●", FontSize = 10, Foreground = (Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 0, 6, 0), ToolTip = "Seleccionado" };
                DockPanel.SetDock(indicator, Dock.Left); caption.Children.Add(indicator);
            }
            caption.Children.Add(new TextBlock { Text = title, FontSize = 11, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetRow(caption, 1); content.Children.Add(caption);
            var button = new Button
            {
                Content = content, Tag = path, Width = 160, Height = 128, Style = _tileStyle,
                Background = (Brush)FindResource(selected ? "SelectedBrush" : "PanelBrush"),
                BorderBrush = (Brush)FindResource(selected ? "AccentBrush" : "BorderBrush"),
                IsEnabled = thumbnail is not null, ToolTip = selected ? title + " · Seleccionado" : title
            };
            AutomationProperties.SetName(button, "Proyectar " + title + (selected ? ", seleccionado" : ""));
            button.Click += (_, _) => { SelectedPath = path; DialogResult = true; };
            var card = new Border { Child = button, Background = Brushes.Transparent, Margin = new Thickness(0, 0, 10, 10) };
            var menu = new ContextMenu();
            var remove = new MenuItem { Header = "Quitar de lista" };
            remove.Click += (_, _) => { Images.Remove(path); _thumbnails.Remove(path); RenderGallery(); };
            menu.Items.Add(remove); card.ContextMenu = menu; button.ContextMenu = menu;
            _gallery.Children.Add(card);
        }
    }

    private void AddImages()
    {
        var picker = new OpenFileDialog
        {
            Title = "Agregar imágenes de fondo", Multiselect = true, CheckFileExists = true,
            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.wdp|Todos los archivos|*.*"
        };
        if (picker.ShowDialog(this) != true) return;
        var failed = 0;
        foreach (var path in picker.FileNames)
        {
            var thumbnail = ReadThumbnail(path);
            if (thumbnail is null) { failed++; continue; }
            _thumbnails[path] = thumbnail;
            if (!Images.Contains(path, StringComparer.OrdinalIgnoreCase)) Images.Add(path);
        }
        _status.Text = failed == 1 ? "No se pudo abrir una imagen." : $"No se pudieron abrir {failed} imágenes.";
        _status.Visibility = failed > 0 ? Visibility.Visible : Visibility.Collapsed;
        RenderGallery();
    }

    private static BitmapSource? ReadThumbnail(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 320;
            image.StreamSource = stream; image.EndInit(); image.Freeze();
            return image;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FormatException or InvalidOperationException or COMException)
        { return null; }
    }

    private static string ImageTitle(string path)
    {
        try { return Path.GetFileNameWithoutExtension(path) is { Length: > 0 } title ? title : "Imagen"; }
        catch (ArgumentException) { return "Imagen"; }
    }
}
