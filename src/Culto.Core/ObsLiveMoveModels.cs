using System.Text.Json;

namespace Culto.Core;

public sealed record ObsLiveLayer(string SourceId, string MapName, int FrameWidth, int FrameHeight,
    double X, double Y, double Width, double Height, double Rotation = 0, double Opacity = 1);

public sealed record ObsLiveMoveCommand(int Width, int Height, int DurationMilliseconds, MoveOptions Move,
    IReadOnlyList<ObsLiveLayer> Outgoing, IReadOnlyList<ObsLiveLayer> Incoming)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static byte[] Encode(ObsLiveMoveCommand command) { command.Validate(); return JsonSerializer.SerializeToUtf8Bytes(command, JsonOptions); }
    public static ObsLiveMoveCommand Decode(ReadOnlySpan<byte> bytes)
    {
        var command = JsonSerializer.Deserialize<ObsLiveMoveCommand>(bytes, JsonOptions) ?? throw new InvalidDataException("Missing live Move command.");
        command.Validate(); return command;
    }
    public void Validate()
    {
        ObsMoveProtocol.FrameByteCount(Width, Height);
        if (DurationMilliseconds is < 1 or > 60_000 || Move is null || Outgoing is null || Incoming is null
            || Outgoing.Count > 128 || Incoming.Count > 128) throw new InvalidDataException("Invalid live Move command.");
        var sources = new Dictionary<string, ObsLiveLayer>(StringComparer.Ordinal);
        foreach (var scene in new[] { Outgoing, Incoming })
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var layer in scene)
            {
                if (layer is null || string.IsNullOrWhiteSpace(layer.SourceId) || layer.SourceId.Length > 200
                    || string.IsNullOrWhiteSpace(layer.MapName) || !layer.MapName.StartsWith("Local\\CultoLive-", StringComparison.Ordinal)
                    || layer.MapName.Length > 200 || !ids.Add(layer.SourceId)
                    || !double.IsFinite(layer.X) || !double.IsFinite(layer.Y) || !double.IsFinite(layer.Width)
                    || !double.IsFinite(layer.Height) || !double.IsFinite(layer.Rotation) || !double.IsFinite(layer.Opacity)
                    || layer.Width <= 0 || layer.Height <= 0 || layer.Width > 65536 || layer.Height > 65536
                    || Math.Abs(layer.X) > 65536 || Math.Abs(layer.Y) > 65536 || Math.Abs(layer.Rotation) > 36000
                    || layer.Opacity is < 0 or > 1) throw new InvalidDataException("Invalid live scene layer.");
                ObsMoveProtocol.FrameByteCount(layer.FrameWidth, layer.FrameHeight);
                if (sources.TryGetValue(layer.SourceId, out var prior)
                    && (prior.MapName != layer.MapName || prior.FrameWidth != layer.FrameWidth || prior.FrameHeight != layer.FrameHeight))
                    throw new InvalidDataException("A shared source must use the same live frame map in both scenes.");
                sources[layer.SourceId] = layer;
            }
        }
    }
}
