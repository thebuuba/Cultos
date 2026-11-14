using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

namespace Culto.Desktop.Dialogs;

public sealed class WindowCaptureDialog : Window
{
    private const int PreviewWidth = 640, PreviewHeight = 360;
    private readonly ListBox _windows = new() { Name = "CaptureWindowList", HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly WindowCaptureCanvas _captureCanvas = new() { Name = "CaptureCanvas" };
    private readonly TextBlock _placeholder = new() { Text = "Selecciona una ventana", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBox _name = new() { Name = "CaptureName" };
    private readonly CheckBox _cursor = new() { Content = "Mostrar cursor", Name = "CaptureCursor", Margin = new Thickness(0, 8, 20, 8) };
    private readonly CheckBox _audio = new() { Content = "Audio de la aplicación", Name = "CaptureAudio", IsChecked = true, Margin = new Thickness(0, 8, 0, 8), ToolTip = "El volumen incluye otras pestañas y ventanas con sonido del mismo navegador." };
    private readonly Cue? _initialCue;
    private readonly WindowCaptureLayout? _youTubeLayout;
    private bool _initialApplied;
    private readonly Button _add;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private ObsWindowCaptureClient? _client;
    private CancellationTokenSource? _cancellation;
    private sealed record PreviewFrame(int Generation, BitmapSource Image);
    private PreviewFrame? _latest;
    private int _generation, _queued;
    private bool _closed, _ready, _refreshing;
    public Cue? SelectedCue { get; private set; }

    public WindowCaptureDialog(Cue? initialCue = null, WindowCaptureLayout? youTubeLayout = null, string? confirmCaption = null)
    {
        _initialCue = initialCue;
        _youTubeLayout = youTubeLayout;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = initialCue is null ? "Capturar ventana" : "Editar captura";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxWidth = Math.Max(1, SystemParameters.WorkArea.Width - 32);
        MaxHeight = Math.Max(1, SystemParameters.WorkArea.Height - 32);
        Width = Math.Min(1120, MaxWidth); Height = Math.Min(760, MaxHeight);
        MinWidth = Math.Min(720, MaxWidth); MinHeight = Math.Min(530, MaxHeight);

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        var refresh = Button("Actualizar", "RefreshCaptureWindows", RefreshWindows);
        DockPanel.SetDock(refresh, Dock.Right); heading.Children.Add(refresh);
        heading.Children.Add(new TextBlock { Text = Title, FontSize = 25, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(heading);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.1, GridUnitType.Star) });
        _windows.ItemTemplate = WindowTemplate();
        _windows.Margin = new Thickness(0, 0, 18, 0);
        ScrollViewer.SetHorizontalScrollBarVisibility(_windows, ScrollBarVisibility.Disabled);
        AutomationProperties.SetName(_windows, "Ventana para capturar");
        _windows.SelectionChanged += (_, _) =>
        {
            if (_refreshing) return;
            ConfigureSelection();
            _ = StartPreview();
        };
        body.Children.Add(_windows);

        var details = new Grid();
        details.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var previewGrid = new Grid { Background = Brushes.Black, ClipToBounds = true };
        previewGrid.Children.Add(_captureCanvas); previewGrid.Children.Add(_placeholder);
        _placeholder.IsHitTestVisible = false;
        _placeholder.Foreground = (Brush)FindResource("MutedBrush");
        details.Children.Add(previewGrid);
        var settings = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        settings.Children.Add(Field("Nombre", _name));
        var checks = new WrapPanel(); checks.Children.Add(_cursor); checks.Children.Add(_audio);
        settings.Children.Add(checks);
        var transforms = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var entry in new[] { ("Ajustar", "FitCapture", Key.F), ("Estirar", "StretchCapture", Key.S),
            ("Centrar", "CenterCapture", Key.D), ("Restablecer", "ResetCapture", Key.R) })
        {
            var key = entry.Item3;
            var button = Button(entry.Item1, entry.Item2, () => _captureCanvas.ApplyShortcut(key));
            button.ToolTip = "Ctrl+" + key;
            button.Margin = new Thickness(0, 0, 8, 0); transforms.Children.Add(button);
        }
        settings.Children.Add(transforms);
        _status.Foreground = (Brush)FindResource("AmberBrush");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        settings.Children.Add(_status);
        Grid.SetRow(settings, 1); details.Children.Add(settings);
        Grid.SetColumn(details, 1); body.Children.Add(details);
        Grid.SetRow(body, 1); root.Children.Add(body);

        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancel = Button("Cancelar", "CancelWindowCapture", () => DialogResult = false); cancel.IsCancel = true;
        _add = Button(confirmCaption ?? (initialCue is null ? "Añadir al orden" : "Guardar captura"), "AddWindowCapture", AddCapture);
        _add.IsDefault = true; _add.IsEnabled = false; _add.Style = (Style)FindResource("PrimaryButton");
        footer.Children.Add(cancel); footer.Children.Add(_add);
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = root;
        _cursor.Checked += (_, _) => SchedulePreview();
        _cursor.Unchecked += (_, _) => SchedulePreview();
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await StartPreview(); };
        Closed += (_, _) => { _closed = true; _debounce.Stop(); StopPreview(); };
        Loaded += (_, _) => RefreshWindows();
    }

    private static Button Button(string caption, string name, Action action)
    {
        var button = new Button { Content = caption, Name = name, Margin = new Thickness(8, 0, 0, 0) };
        button.Click += (_, _) => action();
        AutomationProperties.SetName(button, caption);
        return button;
    }

    private static FrameworkElement Field(string caption, TextBox input)
    {
        AutomationProperties.SetName(input, caption);
        var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 6) };
        panel.Children.Add(new Label { Content = caption, Target = input, Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 6),
            Foreground = (Brush)Application.Current.FindResource("MutedBrush") });
        panel.Children.Add(input); return panel;
    }

    private DataTemplate WindowTemplate()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(MarginProperty, new Thickness(4, 7, 4, 7));
        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(WindowCaptureTarget.DisplayName)));
        title.SetBinding(ToolTipProperty, new Binding(nameof(WindowCaptureTarget.DisplayName)));
        title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); panel.AppendChild(title);
        var detail = new FrameworkElementFactory(typeof(TextBlock));
        detail.SetBinding(TextBlock.TextProperty, new Binding(nameof(WindowCaptureTarget.Detail)));
        detail.SetValue(TextBlock.StyleProperty, FindResource("ItemMetadataText"));
        detail.SetValue(TextBlock.FontSizeProperty, 12d); detail.SetValue(MarginProperty, new Thickness(0, 5, 0, 0));
        panel.AppendChild(detail);
        return new DataTemplate { VisualTree = panel };
    }

    private void RefreshWindows()
    {
        var handle = (_windows.SelectedItem as WindowCaptureTarget)?.Options.WindowHandle ?? _initialCue?.Capture?.WindowHandle;
        try
        {
            var targets = WindowCaptureCatalog.List();
            _refreshing = true;
            _windows.ItemsSource = targets;
            _windows.SelectedItem = targets.FirstOrDefault(target => target.Options.WindowHandle == handle);
            _refreshing = false;
            if (_windows.SelectedItem is WindowCaptureTarget target && (!_captureCanvas.HasSource || _captureCanvas.Options.WindowHandle != target.Options.WindowHandle))
                ConfigureSelection();
            if (targets.Count == 0) SetStatus("No hay ventanas disponibles.");
            _ = StartPreview();
        }
        catch (Exception error) { _refreshing = false; SetStatus(error.Message); }
    }

    private void ConfigureSelection()
    {
        if (_windows.SelectedItem is not WindowCaptureTarget target) return;
        var saved = !_initialApplied ? _initialCue?.Capture : null;
        var options = WindowCaptureCatalog.CopySelection(saved ?? target.Options, target.Options);
        var browserAudio = YouTubeDiscovery.UsesBrowserAudio(target.Options);
        if (browserAudio) options = options with { ControlAudio = false };
        if (_youTubeLayout is not null && YouTubeDiscovery.IsYouTube(target.Options) && !WindowCaptureLayout.IsConfigured(options))
            options = _youTubeLayout.ApplyTo(options);
        _captureCanvas.SetSource(target.Width, target.Height, options);
        _name.Text = !_initialApplied && _initialCue is not null ? _initialCue.Title : target.Options.WindowTitle;
        _cursor.IsChecked = (saved ?? target.Options).CaptureCursor;
        _audio.IsEnabled = !browserAudio;
        _audio.IsChecked = !browserAudio && (saved ?? target.Options).ControlAudio;
        _audio.Content = browserAudio ? "Audio independiente en Chrome" : "Audio de la aplicación";
        _audio.ToolTip = browserAudio ? "Chrome conserva su sonido al cambiar de escena o pestaña. Ajusta su volumen en Chrome o Windows."
            : "El programa controla el volumen de las sesiones de esta aplicación.";
        _initialApplied = true;
    }

    private void SchedulePreview()
    {
        if (_closed) return;
        _ready = false; _add.IsEnabled = false;
        _debounce.Stop(); _debounce.Start();
    }

    private bool ReadOptions(out WindowCaptureOptions? options)
    {
        options = null;
        if (_windows.SelectedItem is not WindowCaptureTarget target) return false;
        if (target.IsMinimized) { SetStatus("Restaura la ventana y pulsa Actualizar."); return false; }
        var draft = _captureCanvas.Options;
        if (draft.CropLeft is < 0 or > 8192 || draft.CropTop is < 0 or > 8192 || draft.CropRight is < 0 or > 8192 || draft.CropBottom is < 0 or > 8192)
        { SetStatus("El recorte no es válido."); return false; }
        var width = _captureCanvas.HasSource ? _captureCanvas.SourceWidth : target.Width;
        var height = _captureCanvas.HasSource ? _captureCanvas.SourceHeight : target.Height;
        if (draft.CropLeft + draft.CropRight >= width || draft.CropTop + draft.CropBottom >= height)
        { SetStatus("El recorte debe conservar una parte visible de la ventana."); return false; }
        options = target.Options with { CaptureCursor = _cursor.IsChecked == true, ControlAudio = _audio.IsEnabled && _audio.IsChecked == true,
            CropLeft = draft.CropLeft, CropTop = draft.CropTop, CropRight = draft.CropRight, CropBottom = draft.CropBottom, Transform = draft.Transform };
        try { options.ValidateTarget(); }
        catch (InvalidDataException error) { SetStatus(error.Message); options = null; return false; }
        return true;
    }

    private async Task StartPreview()
    {
        _debounce.Stop();
        StopPreview();
        if (_closed) return;
        _captureCanvas.Frame = null; _placeholder.Visibility = Visibility.Visible;
        _placeholder.Text = "Selecciona una ventana";
        _status.Visibility = Visibility.Collapsed;
        if (!ReadOptions(out var options)) return;
        if (!ObsWindowCaptureClient.IsAvailable) { SetStatus("El motor de captura no está disponible en esta instalación."); return; }
        _placeholder.Text = "Conectando…";
        int generation = _generation;
        var cancellation = new CancellationTokenSource(); _cancellation = cancellation;
        var client = new ObsWindowCaptureClient(); _client = client;
        try
        {
            var resolved = WindowCaptureCatalog.Resolve(options!);
            // Preview is image-only. It never starts, pauses or adjusts browser audio.
            // Keep one untransformed capture alive while the operator drags. The
            // canvas applies crop/position locally; no worker restart per mouse move.
            await client.CaptureAsync(resolved with { ControlAudio = false, CropLeft = 0, CropTop = 0, CropRight = 0, CropBottom = 0, Transform = null }, PreviewWidth, PreviewHeight, pixels =>
            {
                if (cancellation.IsCancellationRequested) return;
                var bitmap = BitmapSource.Create(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Bgra32, null, pixels, PreviewWidth * 4);
                bitmap.Freeze();
                Interlocked.Exchange(ref _latest, new PreviewFrame(generation, bitmap));
                if (Interlocked.Exchange(ref _queued, 1) != 0) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var frame = Interlocked.Exchange(ref _latest, null);
                    Interlocked.Exchange(ref _queued, 0);
                    if (_closed || frame?.Generation != _generation) return;
                    if (WindowCaptureCatalog.GetSourceSize(resolved.WindowHandle) is { } size)
                        _captureCanvas.UpdateSourceSize(size.Width, size.Height);
                    _captureCanvas.Frame = frame.Image;
                    _placeholder.Visibility = Visibility.Collapsed;
                    _status.Visibility = Visibility.Collapsed;
                    _ready = true; _add.IsEnabled = true;
                }), DispatcherPriority.Render);
            }, cancellation.Token);
            if (!cancellation.IsCancellationRequested) throw new InvalidOperationException("La captura se ha detenido.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_closed && generation == _generation)
            { _generation++; _ready = false; _add.IsEnabled = false; _placeholder.Text = "Captura no disponible"; SetStatus(error.Message); }
        }
        finally
        {
            client.Dispose(); cancellation.Dispose();
            if (ReferenceEquals(_client, client)) _client = null;
            if (ReferenceEquals(_cancellation, cancellation)) _cancellation = null;
        }
    }

    private void StopPreview()
    {
        _generation++; _ready = false; _add.IsEnabled = false;
        var cancellation = _cancellation; _cancellation = null; cancellation?.Cancel();
        var client = _client; _client = null; client?.Dispose();
        Interlocked.Exchange(ref _latest, null);
    }

    private void AddCapture()
    {
        if (!_ready || !ReadOptions(out var options)) return;
        try
        {
            var resolved = WindowCaptureCatalog.Resolve(options!);
            SelectedCue = new Cue { Id = _initialCue?.Id ?? Guid.NewGuid(), Kind = CueKind.WindowCapture, Capture = resolved,
                Title = string.IsNullOrWhiteSpace(_name.Text) ? resolved.WindowTitle : _name.Text.Trim() };
            DialogResult = true;
        }
        catch (Exception error) { SetStatus(error.Message); }
    }

    private void SetStatus(string text) { _status.Text = text; _status.Visibility = Visibility.Visible; }
}
