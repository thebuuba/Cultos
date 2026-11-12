namespace Culto.Core;

public readonly record struct CuePlaybackIntent(bool IsPaused, bool Restart);

// Playback intent survives the outgoing fade and delayed native-show startup.
// Owned by one dispatcher; tokens prevent completion of a replaced request.
public sealed class PendingCuePlayback
{
    public long Token { get; private set; }
    public bool IsPending { get; private set; }
    public bool IsPresented { get; private set; }
    public CuePlaybackIntent Intent { get; private set; }
    private TransitionOptions _transitions = new();
    private BibleVisualStyle _bibleStyle = new();
    public TransitionOptions Transitions => _transitions with { };
    public BibleVisualStyle BibleStyle => _bibleStyle with { };

    public long Begin(TransitionOptions? transitions = null, BibleVisualStyle? bibleStyle = null)
    {
        _transitions = (transitions ?? new()).Normalize();
        _bibleStyle = (bibleStyle ?? new()).Normalize();
        Token++; IsPending = true; IsPresented = false; Intent = default;
        return Token;
    }

    public bool Matches(long token) => IsPending && Token == token;
    public void SetPaused(bool paused)
    {
        if (IsPending) Intent = Intent with { IsPaused = paused };
    }
    public void RequestRestart()
    {
        if (IsPending) Intent = new(false, true);
    }
    public bool MarkPresented(long token)
    {
        if (!Matches(token)) return false;
        IsPresented = true;
        return true;
    }
    public CuePlaybackIntent? Complete(long token)
    {
        if (!Matches(token)) return null;
        var intent = Intent;
        Cancel();
        return intent;
    }
    public void Cancel()
    {
        Token++; IsPending = false; IsPresented = false; Intent = default;
    }
}
