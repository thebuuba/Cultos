using System.Windows;
using Culto.Core;
using Culto.Remote;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private RemoteSnapshot GetRemoteState() => Dispatcher.CheckAccess() ? BuildRemoteState() : Dispatcher.Invoke(BuildRemoteState);
    private RemoteSnapshot BuildRemoteState() => new(
        PlanNameBox.Text, _session.Prepared?.Title ?? "Nada preparado", _outputWindow is null ? "Salida cerrada" : _session.Live?.Title ?? "Fondo",
        _session.IsBlackout, _isPaused, (int)VolumeSlider.Value, _output?.Position ?? 0, _output?.Duration ?? 0, _workspace.Deck.ToArray());

    private async Task<CommandResult> HandleRemote(RemoteCommand command)
        => await Dispatcher.InvokeAsync(() => ExecuteRemote(command));

    private CommandResult ExecuteRemote(RemoteCommand command)
    {
        if (_closing) return new(false, "La consola se está cerrando.");
        try
        {
            var action = command.Action;
            if (action == "deck")
            {
                var button = _workspace.Deck.FirstOrDefault(b => b.Id == command.ButtonId);
                if (button is null) return new(false, "Este botón ya no existe.");
                action = button.Action;
                if (action == "cue")
                {
                    var cue = _workspace.Library.Concat(_plan).FirstOrDefault(c => c.Id.ToString() == button.CueId);
                    if (cue is null) return new(false, "El contenido de este botón no está disponible.");
                    if (!CheckMedia(cue)) return new(false, "No se encuentra el archivo en la computadora.");
                    PrepareCue(cue); ProjectPrepared();
                    return new(_outputWindow is not null && _session.Live?.Id == cue.Id, "Contenido enviado.");
                }
            }
            if (action is not ("project" or "volume") && _outputWindow is null)
                return new(false, "La salida de proyección está cerrada.");
            if (action is "pause" or "restart" or "seek")
            {
                if (_session.Live?.Kind is not (CueKind.Video or CueKind.Audio))
                    return new(false, "No hay un video o audio en pantalla.");
                if (_session.IsBlackout || _session.IsTextHidden)
                    return new(false, "Restaura la imagen antes de controlar la reproducción.");
            }
            switch (action)
            {
                case "project":
                    if (_session.Prepared is null) return new(false, "No hay contenido preparado.");
                    if (!CheckMedia(_session.Prepared)) return new(false, "No se encuentra el archivo en la computadora.");
                    ProjectPrepared();
                    return new(_outputWindow is not null, _outputWindow is null ? "Abre una pantalla de salida en la PC." : "Preparado en pantalla.");
                case "next":
                case "previous":
                    var before = _session.Live?.Id; MoveLive(action == "next" ? 1 : -1);
                    return new(before != _session.Live?.Id, before != _session.Live?.Id ? "Contenido actualizado." : "No hay otro elemento en esa dirección.");
                case "blackout": ToggleBlackout(); break;
                case "background": ShowBackground(); break;
                case "pause": TogglePause(); break;
                case "restart": RestartMedia(); break;
                case "volume": VolumeSlider.Value = Math.Clamp(command.Value ?? 80, 0, 100); break;
                case "seek": _output?.Seek(Math.Clamp(command.Value ?? 0, 0, 1)); break;
                default: return new(false, "Acción no disponible.");
            }
            return new(true, "Acción aplicada.");
        }
        catch (Exception ex) { SetStatus("No se pudo ejecutar el mando: " + ex.Message, true); return new(false, "No se pudo completar la acción. Revisa la consola."); }
    }

    private async void RemoteClick(object sender, RoutedEventArgs e)
    {
        var dialog = new RemoteDialog(_remote, _workspace.Deck, _workspace.Library.Concat(_plan).DistinctBy(c => c.Id).ToArray()) { Owner = this };
        dialog.StartRequested = async () =>
        {
            if (_remote is not null) return _remote;
            var host = new RemoteHost();
            try
            {
                await host.StartAsync(18765, GetRemoteState, HandleRemote);
                _remote = host; RemoteButton.Header = "Mando móvil activo…";
                SetStatus("Mando local activado.");
                return host;
            }
            catch { await host.DisposeAsync(); throw; }
        };
        dialog.StopRequested = async () =>
        {
            if (_remote is null) return;
            var host = _remote; _remote = null;
            await host.DisposeAsync(); RemoteButton.Header = "Mando móvil…";
            SetStatus("Mando desactivado. La proyección continúa.");
        };
        dialog.DeckChanged = buttons => { _workspace.Deck = buttons; SaveWorkspace(); };
        dialog.ShowDialog();
        await Task.CompletedTask;
    }
}
