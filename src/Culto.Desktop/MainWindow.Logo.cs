using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly LogoSlideshow _logoSlides = new();
    private readonly Stopwatch _logoClock = new();
    private readonly DispatcherTimer _logoTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly HashSet<string> _logoUnavailable = new(StringComparer.OrdinalIgnoreCase);
    private bool _logoTimerInitialized, _logoAwaitingOutput;
    private string? _logoCurrentPath;

    private string? CurrentBackgroundPath => _logoSlides.IsRunning ? _logoCurrentPath : _workspace.BackgroundImagePath;

    private void LogoRightClick(object sender, MouseButtonEventArgs e)
    {
        if (ChurchBackgroundButton.ContextMenu is { } menu)
        { e.Handled = true; menu.PlacementTarget = ChurchBackgroundButton; menu.IsOpen = true; }
    }

    private void LogoSettingsClick(object sender, RoutedEventArgs e)
    {
        var images = _workspace.BackgroundImages.Concat(_workspace.BackgroundImagePath is { } path ? [path] : Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var dialog = new LogoSettingsDialog(images, _workspace.BackgroundImagePath, _workspace.LogoPlayback) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var previousImages = _workspace.BackgroundImages;
        var previousPath = _workspace.BackgroundImagePath;
        var previousOptions = _workspace.LogoPlayback;
        _workspace.BackgroundImages = dialog.Images.ToList();
        _workspace.BackgroundImagePath = dialog.SelectedPath;
        _workspace.LogoPlayback = dialog.Options.Normalize();
        if (!SaveWorkspace())
        {
            _workspace.BackgroundImages = previousImages;
            _workspace.BackgroundImagePath = previousPath;
            _workspace.LogoPlayback = previousOptions;
            return;
        }
        if (_logoSlides.IsRunning && _session.IsTextHidden && !_session.IsBlackout
            && !StartLogoPresentation(_logoCurrentPath)) return;
        SetStatus("Logo guardado.");
    }

    private IEnumerable<string> AvailableLogoPaths() => _workspace.BackgroundImages
        .Concat(_workspace.BackgroundImagePath is { } selected ? [selected] : Array.Empty<string>())
        .Where(path => !string.IsNullOrWhiteSpace(path) && !_logoUnavailable.Contains(path) && File.Exists(path))
        .Distinct(StringComparer.OrdinalIgnoreCase);

    private string? InitialLogoPath() => AvailableLogoPaths()
        .FirstOrDefault(path => string.Equals(path, _workspace.BackgroundImagePath, StringComparison.OrdinalIgnoreCase))
        ?? AvailableLogoPaths().FirstOrDefault();

    private bool ShowLogo()
    {
        _preserveAudioForBackdrop = false;
        return StartLogoPresentation(InitialLogoPath());
    }

    private void ShowStartupLogo()
    {
        if (ShowLogo()) return;
        // An unavailable logo must never cause a saved hymn to start instead.
        if (EnsureOutput())
        {
            _session.ShowBlackout();
            ApplyBackdropTransition();
        }
        SetStatus("Logo no disponible. Configura una imagen en Logo; el inicio permanece en negro y sin audio.", true);
    }

    private bool StartLogoPresentation(string? preferredPath)
    {
        var wasActive = _logoSlides.IsRunning && _session.IsTextHidden && !_session.IsBlackout;
        var previousPath = _logoCurrentPath;
        var wasAwaitingOutput = _logoAwaitingOutput;
        StopLogo();
        _logoUnavailable.Clear();
        var path = _logoSlides.Start(AvailableLogoPaths(), preferredPath, _workspace.LogoPlayback);
        if (path is null)
        {
            if (wasActive && _output is not null)
            {
                _logoCurrentPath = null;
                _output.BackgroundImagePath = null;
                ApplyBackdropTransition();
            }
            SetStatus("Logo no tiene imágenes disponibles.", true);
            return false;
        }
        if (!EnsureOutput()) { StopLogo(); return false; }
        if (!TrySelectLogoFrame(path)) { StopLogo(); return false; }
        if (!_logoTimerInitialized)
        {
            _logoTimerInitialized = true;
            _logoTimer.Tick += (_, _) => AdvanceLogo();
        }
        _session.ShowBackground();
        _logoClock.Restart();
        _logoAwaitingOutput = wasAwaitingOutput;
        if (!wasActive || !_preserveAudioForBackdrop || !string.Equals(previousPath, _logoCurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            _logoAwaitingOutput = true;
            ApplyBackdropTransition();
        }
        _logoTimer.Start();
        UpdateSessionLabels();
        SetStatus("Logo");
        return true;
    }

    private bool TrySelectLogoFrame(string path)
    {
        while (true)
        {
            if (CheckMedia(new Cue { Kind = CueKind.Image, Title = "Logo", FilePath = path }))
            { _logoCurrentPath = path; return true; }
            _logoUnavailable.Add(path);
            var next = _logoSlides.Start(AvailableLogoPaths(), null, _workspace.LogoPlayback);
            if (next is null) return false;
            path = next;
        }
    }

    private void AdvanceLogo()
    {
        if (_closing || _output is null || !_session.IsTextHidden || _session.IsBlackout)
        { StopLogo(); return; }
        if (!_logoSlides.IsRunning || _logoAwaitingOutput || _pendingCuePlayback.IsPending) return;
        var next = _logoSlides.Advance(_logoClock.ElapsedMilliseconds, _output.IsTransitioning);
        if (next is null) return;
        if (!TrySelectLogoFrame(next)) { StopLogo(); SetStatus("Logo no tiene imágenes disponibles.", true); return; }
        _logoAwaitingOutput = true;
        ApplyBackdropTransition();
    }

    private void StopLogo()
    {
        _logoTimer.Stop(); _logoClock.Stop(); _logoSlides.Stop();
        _logoAwaitingOutput = false;
    }

    private void SyncLogoPlayback()
    {
        if (_logoSlides.IsRunning && (_output is null || !_session.IsTextHidden || _session.IsBlackout)) StopLogo();
    }

    private void RestoreLogoContentClick(object sender, RoutedEventArgs e)
    {
        if (_output is null) return;
        StopLogo(); _session.RestoreContent(); ApplyBackdropTransition();
    }
}
