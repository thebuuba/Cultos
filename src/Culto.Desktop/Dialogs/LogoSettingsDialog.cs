using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Culto.Core;
using Microsoft.Win32;

namespace Culto.Desktop.Dialogs;

public sealed class LogoSettingsDialog : Window
{
    private readonly List<string> _images;
    private readonly ListBox _gallery = new() { Name = "LogoImages", HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _empty = new() { Text = "Sin imágenes", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private readonly TextBlock _status = new() { Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBox _interval = new() { Name = "LogoIntervalSeconds", MinWidth = 110 };
    private readonly TextBox _transition = new() { Name = "LogoTransitionMilliseconds", MinWidth = 110 };
    private readonly Button _remove;
    private readonly Button _up;
    private readonly Button _down;
    private LogoPlaybackOptions _options;

    public List<string> Images => [.. _images];
    public string? SelectedPath { get; private set; }
    public LogoPlaybackOptions Options => _options with { };

    public LogoSettingsDialog(IReadOnlyList<string> images, string? selectedPath, LogoPlaybackOptions options)
    {
        _images = images.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        SelectedPath = _images.FirstOrDefault(path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase)) ?? _images.FirstOrDefault();
        _options = options.Normalize();
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Logo";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxWidth = Math.Max(1, SystemParameters.WorkArea.Width - 32);
        MaxHeight = Math.Max(1, SystemParameters.WorkArea.Height - 32);
        Width = Math.Min(660, MaxWidth); Height = Math.Min(620, MaxHeight);
        MinWidth = Math.Min(390, MaxWidth); MinHeight = Math.Min(440, MaxHeight);

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = Title, FontSize = 25, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var add = ActionButton("+ Agregar", "AddLogoImages", AddImages);
        add.Style = (Style)FindResource("PrimaryButton");
        _remove = ActionButton("Quitar", "RemoveLogoImage", RemoveSelected);
        _up = ActionButton("↑ Subir", "MoveLogoImageUp", () => MoveSelected(-1));
        _down = ActionButton("↓ Bajar", "MoveLogoImageDown", () => MoveSelected(1));
        actions.Children.Add(add); actions.Children.Add(_remove); actions.Children.Add(_up); actions.Children.Add(_down);
        heading.Children.Add(actions);
        heading.Children.Add(new TextBlock { Text = "Imagen inicial", Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 0, 0, 6) });
        root.Children.Add(heading);

        _gallery.ItemTemplate = CreateImageTemplate();
        _gallery.SelectionChanged += (_, _) =>
        {
            SelectedPath = (_gallery.SelectedItem as LogoImage)?.Path;
            UpdateActions();
        };
        AutomationProperties.SetName(_gallery, "Imágenes del logo e imagen inicial");
        ScrollViewer.SetHorizontalScrollBarVisibility(_gallery, ScrollBarVisibility.Disabled);
        var galleryArea = new Grid();
        galleryArea.Children.Add(_gallery);
        _empty.Foreground = (Brush)FindResource("MutedBrush");
        galleryArea.Children.Add(_empty);
        Grid.SetRow(galleryArea, 1); root.Children.Add(galleryArea);

        var settings = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        var fields = new WrapPanel();
        _interval.Text = _options.IntervalSeconds.ToString("0.###", CultureInfo.CurrentCulture);
        _transition.Text = _options.TransitionMilliseconds.ToString(CultureInfo.CurrentCulture);
        fields.Children.Add(Field("Tiempo por imagen (s)", _interval));
        fields.Children.Add(Field("Duración de transición (ms)", _transition));
        settings.Children.Add(fields);
        _status.Foreground = (Brush)FindResource("AmberBrush");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        settings.Children.Add(_status);
        Grid.SetRow(settings, 2); root.Children.Add(settings);

        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancel = ActionButton("Cancelar", "CancelLogoSettings", () => DialogResult = false);
        cancel.IsCancel = true;
        var save = ActionButton("Guardar", "SaveLogoSettings", Save);
        save.Style = (Style)FindResource("PrimaryButton"); save.IsDefault = true;
        footer.Children.Add(cancel); footer.Children.Add(save);
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        Content = root;
        RenderImages();
    }

    private static Button ActionButton(string caption, string name, Action action)
    {
        var button = new Button { Content = caption, Name = name, Margin = new Thickness(0, 0, 8, 6) };
        button.Click += (_, _) => action();
        AutomationProperties.SetName(button, caption.TrimStart('↑', '↓', '+', ' '));
        return button;
    }

    private static FrameworkElement Field(string caption, TextBox input)
    {
        var field = new StackPanel { Margin = new Thickness(0, 0, 20, 6) };
        var label = new Label { Content = caption, Target = input, Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 6) };
        AutomationProperties.SetName(input, caption);
        field.Children.Add(label); field.Children.Add(input);
        return field;
    }

    private DataTemplate CreateImageTemplate()
    {
        var row = new FrameworkElementFactory(typeof(DockPanel));
        row.SetValue(DockPanel.LastChildFillProperty, true);
        var thumbnail = new FrameworkElementFactory(typeof(MediaThumbnail));
        thumbnail.SetValue(WidthProperty, 96d); thumbnail.SetValue(HeightProperty, 54d);
        thumbnail.SetValue(MarginProperty, new Thickness(0, 0, 14, 0));
        thumbnail.SetValue(DockPanel.DockProperty, Dock.Left);
        thumbnail.SetBinding(MediaThumbnail.CueProperty, new Binding(nameof(LogoImage.Cue)));
        row.AppendChild(thumbnail);
        var caption = new FrameworkElementFactory(typeof(TextBlock));
        caption.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        caption.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        caption.SetBinding(TextBlock.TextProperty, new Binding(nameof(LogoImage.Title)));
        caption.SetBinding(ToolTipProperty, new Binding(nameof(LogoImage.Path)));
        row.AppendChild(caption);
        return new DataTemplate { VisualTree = row };
    }

    private void RenderImages()
    {
        var selected = SelectedPath;
        var rows = _images.Select(path => new LogoImage(path)).ToList();
        _gallery.ItemsSource = rows;
        _gallery.SelectedItem = rows.FirstOrDefault(row => string.Equals(row.Path, selected, StringComparison.OrdinalIgnoreCase)) ?? rows.FirstOrDefault();
        SelectedPath = (_gallery.SelectedItem as LogoImage)?.Path;
        if (_gallery.SelectedItem is not null) _gallery.ScrollIntoView(_gallery.SelectedItem);
        _empty.Visibility = _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateActions();
    }

    private void UpdateActions()
    {
        var index = _gallery.SelectedIndex;
        _remove.IsEnabled = index >= 0;
        _up.IsEnabled = index > 0;
        _down.IsEnabled = index >= 0 && index < _images.Count - 1;
    }

    private void AddImages()
    {
        var picker = new OpenFileDialog
        {
            Title = "Agregar imágenes al logo", Multiselect = true, CheckFileExists = true,
            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.wdp"
        };
        if (picker.ShowDialog(this) != true) return;
        foreach (var path in picker.FileNames)
            if (!_images.Contains(path, StringComparer.OrdinalIgnoreCase)) _images.Add(path);
        RenderImages();
    }

    private void RemoveSelected()
    {
        var index = _gallery.SelectedIndex;
        if (index < 0 || index >= _images.Count) return;
        _images.RemoveAt(index);
        SelectedPath = _images.Count == 0 ? null : _images[Math.Min(index, _images.Count - 1)];
        RenderImages();
    }

    private void MoveSelected(int offset)
    {
        var index = _gallery.SelectedIndex; var target = index + offset;
        if (index < 0 || target < 0 || target >= _images.Count) return;
        (_images[index], _images[target]) = (_images[target], _images[index]);
        RenderImages();
    }

    private void Save()
    {
        if ((!double.TryParse(_interval.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var interval)
            && !double.TryParse(_interval.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out interval))
            || !double.IsFinite(interval) || interval < 1 || interval > 3600)
        {
            ShowError("Tiempo por imagen: entre 1 y 3600 segundos.", _interval); return;
        }
        if (!int.TryParse(_transition.Text, out var transition) || transition < 100 || transition > 3000)
        {
            ShowError("Duración de transición: entre 100 y 3000 ms.", _transition); return;
        }
        _options = new LogoPlaybackOptions { IntervalSeconds = interval, TransitionMilliseconds = transition }.Normalize();
        DialogResult = true;
    }

    private void ShowError(string message, TextBox field)
    {
        _status.Text = message; _status.Visibility = Visibility.Visible;
        field.Focus(); field.SelectAll();
    }

    private sealed class LogoImage
    {
        public string Path { get; }
        public string Title { get; }
        public Cue Cue { get; }
        public LogoImage(string path)
        {
            Path = path;
            Title = System.IO.Path.GetFileNameWithoutExtension(path);
            Cue = new Cue { Title = Title, Kind = CueKind.Image, FilePath = path };
        }
    }
}
