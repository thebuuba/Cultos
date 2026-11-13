namespace Culto.Remote;

public sealed record DeckButton(string Id, string Label, string Color, string Action, string? CueId = null);
public sealed record RemoteCue(string Id, string Title, string Kind, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Body = null);
public sealed record RemoteClientInfo(string Id, string Name, DateTimeOffset LastSeen, bool IsConnected);
public sealed record RemoteSnapshot(string PlanName, string PreparedTitle, string LiveTitle,
    bool IsBlackout, bool IsPaused, int Volume, double Position, double Duration, IReadOnlyList<DeckButton> Buttons,
    IReadOnlyList<RemoteCue>? Order = null, string? LiveCueId = null, string? PreparedCueId = null,
    bool HasOutput = false, bool IsBackground = false, bool HasExternalOutput = false, bool CanPause = true,
    string? LiveBody = null, string? LiveReference = null, string? LiveKind = null);
public sealed record RemoteCommand(string Action, string RequestId, string? ButtonId = null, double? Value = null,
    string? CueId = null);
public sealed record CommandResult(bool Success, string Message);
