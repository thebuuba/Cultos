namespace Culto.Core;

public sealed class PresentationSession
{
    public Cue? Prepared { get; private set; }
    public Cue? Live { get; private set; }
    // Compare rendered content, not just the ID: an edited copy must stay prepared
    // until it is sent, while selecting the unchanged live item follows its output.
    public bool PreviewFollowsLive => Prepared is null || Live is not null
        && Prepared.Id == Live.Id && Prepared.Kind == Live.Kind
        && Prepared.Title == Live.Title && Prepared.Body == Live.Body
        && Prepared.Reference == Live.Reference && Prepared.Version == Live.Version
        && Prepared.FilePath == Live.FilePath && Prepared.YouTubeUrl == Live.YouTubeUrl && Prepared.Capture == Live.Capture && Prepared.Browser == Live.Browser;
    public bool IsBlackout { get; private set; }
    public bool IsTextHidden { get; private set; }
    public event Action? Changed;
    public void FollowLive()
    {
        Prepared = Live is null ? null : Live.Copy();
        Changed?.Invoke();
    }
    public void Prepare(Cue cue)
    {
        ArgumentNullException.ThrowIfNull(cue);
        Prepared = cue;
        Changed?.Invoke();
    }

    public void GoLive()
    {
        if (Prepared is null) return;
        Live = Prepared.Copy();
        IsBlackout = false;
        IsTextHidden = false;
        Changed?.Invoke();
    }

    public void RestoreOutput(Cue? cue, bool blackout, bool hidden)
    {
        Live = cue is null ? null : cue.Copy();
        IsBlackout = blackout;
        IsTextHidden = hidden;
        Changed?.Invoke();
    }

    public void ToggleBlackout()
    {
        IsBlackout = !IsBlackout;
        Changed?.Invoke();
    }

    public void ShowBackground()
    {
        IsTextHidden = true;
        IsBlackout = false;
        Changed?.Invoke();
    }

    public void RestoreContent()
    {
        IsBlackout = false;
        IsTextHidden = false;
        Changed?.Invoke();
    }

    public void ShowBlackout()
    {
        IsBlackout = true;
        IsTextHidden = false;
        Changed?.Invoke();
    }

    public bool Move(ServicePlan plan, int delta)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (Live is null || delta == 0) return false;
        var current = plan.Items.FindIndex(cue => cue.Id == Live.Id);
        var next = (long)current + delta;
        if (current < 0 || next < 0 || next >= plan.Items.Count) return false;
        Prepared = plan.Items[(int)next];
        GoLive();
        return true;
    }
}
