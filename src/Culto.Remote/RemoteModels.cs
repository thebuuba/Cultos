namespace Culto.Remote;

public sealed record DeckButton(string Id, string Label, string Color, string Action, string? CueId = null);
public sealed record RemoteCue(string Id, string Title, string Kind);
public sealed record RemoteSnapshot(string PlanName, string PreparedTitle, string LiveTitle,
    bool IsBlackout, bool IsPaused, int Volume, double Position, double Duration, IReadOnlyList<DeckButton> Buttons,
    IReadOnlyList<RemoteCue>? Order = null, string? LiveCueId = null, string? PreparedCueId = null,
    bool HasOutput = false, bool IsBackground = false, bool HasExternalOutput = false);
public sealed record RemoteCommand(string Action, string RequestId, string? ButtonId = null, double? Value = null,
    string? CueId = null);
public sealed record CommandResult(bool Success, string Message);
