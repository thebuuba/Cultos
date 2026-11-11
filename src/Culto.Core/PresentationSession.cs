namespace Culto.Core;

public sealed class PresentationSession
{
    public Cue? Prepared { get; private set; }
    public Cue? Live { get; private set; }
    public bool IsBlackout { get; private set; }
    public bool IsTextHidden { get; private set; }
    public event Action? Changed;
    public void Prepare(Cue cue)
    {
        ArgumentNullException.ThrowIfNull(cue);
        Prepared = cue;
        Changed?.Invoke();
    }

    public void GoLive()
    {
        if (Prepared is null) return;
        Live = new Cue
        {
            Id = Prepared.Id, Title = Prepared.Title, Kind = Prepared.Kind,
            Body = Prepared.Body, Reference = Prepared.Reference, Version = Prepared.Version,
            FilePath = Prepared.FilePath, HymnNumber = Prepared.HymnNumber, Notes = Prepared.Notes
        };
        IsBlackout = false;
        IsTextHidden = false;
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
