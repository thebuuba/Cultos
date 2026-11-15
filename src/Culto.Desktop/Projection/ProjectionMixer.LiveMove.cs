using System;
using System.Windows.Controls;

namespace Culto.Desktop.Projection;

internal sealed partial class ProjectionMixer
{
    private ObsLiveMoveClient? _liveMoveClient;
    private LiveMoveTransition? _liveMove;
    private void BeginLiveMove(Request request)
    {
        EndLiveMove();
        if (_outgoing is null || ActualWidth < 1 || ActualHeight < 1) return;
        _liveMoveClient ??= new ObsLiveMoveClient();
        _liveMove = new LiveMoveTransition(_liveMoveClient, _outgoing, _current, _outgoingCue, _currentCue,
            request.Options, ((int)Math.Ceiling(ActualWidth) + 3) & ~3, ((int)Math.Ceiling(ActualHeight) + 1) & ~1,
            message => { _liveMoveClient?.Dispose(); _liveMoveClient = null; PlaybackError?.Invoke(message); });
        Panel.SetZIndex(_liveMove.Overlay, 1000); Children.Add(_liveMove.Overlay);
    }
    private void EndLiveMove()
    {
        if (_liveMove is not { } move) return;
        _liveMove = null; Children.Remove(move.Overlay); move.Dispose();
    }
}
