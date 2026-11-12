using System.Windows;
using Culto.Core;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void ChurchBackgroundClick(object sender, RoutedEventArgs e)
    {
        var paths = _workspace.BackgroundImages
            .Concat(_workspace.BackgroundImagePath is { } selected ? new[] { selected } : Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var dialog = new BackgroundGalleryDialog(paths, _workspace.BackgroundImagePath,
            _output is not null && (_session.IsTextHidden || _session.IsBlackout)) { Owner = this };
        var chosen = dialog.ShowDialog() == true;
        _workspace.BackgroundImages = dialog.Images;
        if (_workspace.BackgroundImagePath is { } previous && !dialog.Images.Contains(previous, StringComparer.OrdinalIgnoreCase))
            _workspace.BackgroundImagePath = null;
        SaveWorkspace();
        if (!chosen) return;
        if (dialog.RestoreContent)
        {
            _session.RestoreContent(); ApplyBackdropTransition(); return;
        }
        if (dialog.SelectedPath is { } path && !CheckMedia(new Cue { Kind = CueKind.Image, Title = "Fondo iglesia", FilePath = path })) return;
        var alreadyShowing = _output is not null && _session.IsTextHidden && !_session.IsBlackout;
        SetChurchBackground(dialog.SelectedPath);
        if (!alreadyShowing)
        {
            if (!EnsureOutput()) return;
            _session.ShowBackground(); ApplyBackdropTransition();
        }
    }
    private void BlackBackgroundClick(object sender, RoutedEventArgs e) => ShowBlackBackdrop();

    private void ShowBlackBackdrop()
    {
        if (!EnsureOutput()) return;
        if (_session.IsBlackout) _session.RestoreContent();
        else _session.ShowBlackout();
        ApplyBackdropTransition();
    }

    private void ApplyBackdropTransition()
    {
        if (_pendingCuePlayback.IsPending)
        {
            _pendingCuePlayback.SetPaused(true);
            _isPaused = true;
            if (!_pendingCuePlayback.IsPresented)
            {
                var token = _pendingCuePlayback.Token;
                FadePowerPointOut(() => CommitPendingCue(token), _pendingCuePlayback.Transitions.AudioFadeOutMilliseconds);
                return;
            }
        }
        var transitions = _workspace.Transitions.Normalize();
        FadePowerPointOut(() => ApplyBackdropTransitionCore(transitions), transitions.AudioFadeOutMilliseconds);
    }

    private void ApplyBackdropTransitionCore(TransitionOptions transitions)
    {
        string? presentationError = null;
        if (_powerPoint?.IsRunning == true)
        {
            // Hide the native window before COM playback calls, which can be rejected
            // while PowerPoint is busy. A rejected pause must never defeat blackout.
            RoutePowerPointOutput();
            try { _powerPoint.SetPaused(true); PowerPointPauseButton.Content = "Continuar"; }
            catch (Exception error)
            {
                StopPowerPointOutput(); presentationError = "PowerPoint se cerró al detener la reproducción: " + error.Message;
            }
        }
        _output!.BackgroundImagePath = _workspace.BackgroundImagePath;
        _output.Transitions = transitions;
        _output.TransitionToBackdrop(_session.Live, _session.IsBlackout, _session.IsTextHidden);
        _isPaused = true;
        UpdateSessionLabels();
        SetStatus(presentationError ?? (_session.IsBlackout ? "Fondo negro" : _session.IsTextHidden ? "Fondo iglesia" : "Contenido restaurado; reproducción pausada."), presentationError is not null);
    }

    private void ChangeChurchBackgroundClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Fondo de la iglesia", CheckFileExists = true,
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"
        };
        if (dialog.ShowDialog(this) != true) return;
        if (!CheckMedia(new Cue { Kind = CueKind.Image, Title = "Fondo iglesia", FilePath = dialog.FileName })) return;
        SetChurchBackground(dialog.FileName);
    }

    private void ClearChurchBackgroundClick(object sender, RoutedEventArgs e) => SetChurchBackground(null);

    private void SetChurchBackground(string? path)
    {
        _workspace.BackgroundImagePath = path;
        if (path is not null && !_workspace.BackgroundImages.Contains(path, StringComparer.OrdinalIgnoreCase))
            _workspace.BackgroundImages.Add(path);
        SaveWorkspace();
        if (_output is not null)
        {
            _output.BackgroundImagePath = path;
            if (_session.IsTextHidden && !_session.IsBlackout) ApplyBackdropTransition();
        }
        SetStatus(path is null ? "Fondo sencillo seleccionado." : "Fondo de la iglesia guardado.");
    }
}
