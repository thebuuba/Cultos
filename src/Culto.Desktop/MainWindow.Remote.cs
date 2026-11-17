using System.Windows;
using Culto.Core;
using Culto.Remote;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private RemoteSnapshot GetRemoteState() => Dispatcher.CheckAccess() ? BuildRemoteState() : Dispatcher.Invoke(BuildRemoteState);
    private RemoteSnapshot BuildRemoteState() => new(
        PlanNameBox.Text, _session.Prepared?.Title ?? "Nada preparado", _output is null ? "Salida cerrada" : (_outputWindow is null ? "Local · " : "") + (_session.Live?.Title ?? "Fondo"),
        _output is not null && _session.IsBlackout, _pendingCuePlayback.IsPending ? _session.IsBlackout || _session.IsTextHidden || _pendingCuePlayback.Intent.IsPaused
            : _session.Live?.Kind == CueKind.PowerPoint ? _isPaused : _output?.IsPaused ?? _isPaused,
        (int)VolumeSlider.Value, _output?.Position ?? 0, _output?.Duration ?? 0, _workspace.Deck.ToArray(),
        _plan.Select(cue => new RemoteCue(cue.Id.ToString(), cue.Title, cue.Kind.ToString())).ToArray(),
        _output is null ? null : _session.Live?.Id.ToString(), _session.Prepared?.Id.ToString(),
        _output is not null, _output is not null && _session.IsTextHidden, _outputWindow is not null,
        CanControlFileMedia || _output is not null && _session.Live?.Kind == CueKind.PowerPoint
            && !_session.IsBlackout && !_session.IsTextHidden,
        _output is not null && !_session.IsBlackout && !_session.IsTextHidden && _session.Live?.Kind is CueKind.Bible or CueKind.Text
            ? _session.Live.Body : null,
        _output is not null && !_session.IsBlackout && !_session.IsTextHidden ? _session.Live?.Reference : null,
        _output is not null ? _session.Live?.Kind.ToString() : null, BuildRemoteScenes(),
        VisualChannelTitle, AudioChannelTitle, _output?.HasBackgroundMedia == true, _session.Prepared?.Kind.ToString());

    private async Task<CommandResult> HandleRemote(RemoteCommand command)
        => await Dispatcher.InvokeAsync(() => ExecuteRemote(command));

    private CommandResult ExecuteRemote(RemoteCommand command)
    {
        if (_closing) return new(false, "La consola se está cerrando.");
        try
        {
            var action = command.Action;
            if (action == "scene") return ExecuteRemoteSceneMode(command.ButtonId, command.PreserveOther == true);
            if (action == "return-media")
            {
                if (_output?.HasBackgroundMedia != true) return new(false, "No hay video de fondo al que volver.");
                ReturnToMedia(); return new(true, "Volviendo al video en su posición actual.");
            }
            if (action == "library")
            {
                if (!Guid.TryParse(command.CueId, out var id) || !_remoteCatalog.TryGetValue(id, out var selected))
                    return new(false, "Busca de nuevo este contenido en el teléfono.");
                if (!CheckMedia(selected)) return new(false, "El archivo no está disponible en la PC.");
                PrepareCue(selected, selected.Kind == CueKind.Bible ? _bibleReading : null);
                if (command.PreserveOther == true) return new(ProjectPreparedPreservingOther(), StatusText.Text);
                ProjectPrepared();
                return new(_output is not null && _session.Live?.Id == selected.Id, "Contenido enviado a pantalla.");
            }
            if (action == "cue")
            {
                var cue = Guid.TryParse(command.CueId, out var cueId) ? _plan.FirstOrDefault(c => c.Id == cueId) : null;
                if (cue is null) return new(false, "Este elemento ya no pertenece al orden del culto.");
                if (!CheckMedia(cue)) return new(false, "No se encuentra el archivo en la computadora.");
                PrepareCue(cue);
                if (command.PreserveOther == true) return new(ProjectPreparedPreservingOther(), StatusText.Text);
                ProjectPrepared();
                var projected = _output is not null && _session.Live?.Id == cue.Id;
                return new(projected, !projected ? "No se pudo iniciar la salida."
                    : _outputWindow is null ? "Contenido enviado a la salida local." : "Contenido enviado a pantalla.");
            }
            if (action == "deck")
            {
                var button = _workspace.Deck.FirstOrDefault(b => b.Id == command.ButtonId);
                if (button is null) return new(false, "Este botón ya no existe.");
                action = button.Action;
                if (action == "cue")
                {
                    var cue = _workspace.Library.Concat(_workspace.YouTubeSources).Concat(_plan).FirstOrDefault(c => c.Id.ToString() == button.CueId);
                    if (cue is null) return new(false, "El contenido de este botón no está disponible.");
                    if (!CheckMedia(cue)) return new(false, "No se encuentra el archivo en la computadora.");
                    PrepareCue(cue); ProjectPrepared();
                    return new(_output is not null && _session.Live?.Id == cue.Id, "Contenido enviado.");
                }
            }
            if (action is not ("project" or "volume" or "volume-step" or "background" or "blackout") && _output is null)
                return new(false, "La salida de proyección está cerrada.");
            if (action is "pause" or "restart" or "seek" && !HasBackgroundFileAudio)
            {
                if (_session.Live?.Kind == CueKind.WindowCapture)
                    return new(false, "La reproducción de esta captura se controla desde YouTube.");
                if (_session.Live?.Kind == CueKind.PowerPoint && action == "seek")
                    return new(false, "Usa Anterior y Siguiente para recorrer PowerPoint.");
                if (_session.Live?.Kind is not (CueKind.Video or CueKind.Audio or CueKind.PowerPoint))
                    return new(false, "No hay un video o audio en pantalla.");
                if (_session.IsBlackout || _session.IsTextHidden)
                    return new(false, "Restaura la imagen antes de controlar la reproducción.");
            }
            switch (action)
            {
                case "project":
                    if (_session.Prepared is null) return new(false, "No hay contenido preparado.");
                    if (!CheckMedia(_session.Prepared)) return new(false, "No se encuentra el archivo en la computadora.");
                    if (command.PreserveOther == true) return new(ProjectPreparedPreservingOther(), StatusText.Text);
                    ProjectPrepared();
                    return new(_output is not null, _output is null ? "No se pudo iniciar la salida." : _outputWindow is null ? "Preparado en salida local." : "Preparado en pantalla.");
                case "next":
                case "previous":
                    if (_session.Live?.Kind == CueKind.PowerPoint && _powerPoint is not null)
                    {
                        if (_session.IsBlackout || _session.IsTextHidden) return new(false, "Restaura el contenido.");
                        var advanced = AdvancePowerPointWithFade(action == "next" ? 1 : -1);
                        UpdatePowerPointPosition();
                        return new(advanced, advanced ? "Presentación actualizada." : "Fin de la presentación.");
                    }
                    var before = _session.Live?.Id; AdvancePresentation(action == "next" ? 1 : -1);
                    return new(before != _session.Live?.Id, before != _session.Live?.Id ? "Contenido actualizado." : "No hay otro elemento en esa dirección.");
                case "blackout": ShowBlackBackdrop(); break;
                case "background":
                    return new(ShowLogo(), _logoSlides.IsRunning ? "Logo en pantalla." : "Logo no tiene imágenes disponibles.");
                case "pause": TogglePause(); break;
                case "restart": RestartMedia(); break;
                case "volume": VolumeSlider.Value = Math.Clamp(command.Value ?? 80, 0, 100); break;
                case "volume-step": VolumeSlider.Value = Math.Clamp(VolumeSlider.Value + (command.Value ?? 0), 0, 100); break;
                case "seek": _output?.Seek(Math.Clamp(command.Value ?? 0, 0, 1)); break;
                default: return new(false, "Acción no disponible.");
            }
            return new(true, "Acción aplicada.");
        }
        catch (Exception ex) { SetStatus("No se pudo ejecutar el mando: " + ex.Message, true); return new(false, "No se pudo completar la acción. Revisa la consola."); }
    }

    private async void RemoteClick(object sender, RoutedEventArgs e)
    {
        var dialog = new RemoteDialog(_remote, _workspace.Deck, _workspace.Library.Concat(_workspace.YouTubeSources).Concat(_plan).DistinctBy(c => c.Id).ToArray()) { Owner = this };
        dialog.SnapshotRequested = GetRemoteState;
        dialog.StartRequested = async () =>
        {
            if (_remote is not null) return _remote;
            var host = new RemoteHost();
            try
            {
                await host.StartAsync(18765, GetRemoteState, HandleRemote, catalog: SearchRemoteCatalog);
                _remote = host; RemoteButton.Header = "Mando móvil activo…";
                UpdateWorkspaceHeader();
                SetStatus("Mando local activado.");
                return host;
            }
            catch { await host.DisposeAsync(); throw; }
        };
        dialog.StopRequested = async () =>
        {
            if (_remote is null) return;
            var host = _remote; _remote = null;
            UpdateWorkspaceHeader();
            await host.DisposeAsync(); RemoteButton.Header = "Mando móvil…";
            SetStatus("Mando desactivado. La proyección continúa.");
        };
        dialog.DeckChanged = buttons => { _workspace.Deck = buttons; SaveWorkspace(); };
        dialog.ShowDialog();
        await Task.CompletedTask;
    }
}
