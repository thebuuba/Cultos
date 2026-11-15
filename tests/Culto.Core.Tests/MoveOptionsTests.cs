using System.Text.Json;
using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public class MoveOptionsTests
{
    [Fact]
    public void Move_settings_survive_the_native_command_boundary()
    {
        var options = new MoveOptions { In = new MoveItemOptions { Position = 18, Zoom = 75, Curve = .6, Easing = 2, Function = 9 },
            Out = new MoveItemOptions { Position = 34, Zoom = 150, Curve = -.5, Easing = 1, Function = 10 } };
        var command = new ObsMoveCommand("before.png", "after.png", 800, options);
        Assert.Equal(options, ObsMoveProtocol.DecodeCommand(ObsMoveProtocol.EncodeCommand(command)).Move);
    }

    [Fact]
    public void Normalization_bounds_native_settings_and_detaches_transition_drafts()
    {
        var source = new TransitionOptions { Kind = "Move", Move = new MoveOptions
            { In = new MoveItemOptions { Position = 999, Zoom = double.NaN, Curve = double.PositiveInfinity, Easing = 99, Function = -1 } } };
        var copy = source.Normalize();
        Assert.Equal(6, copy.Move.In.Position);
        Assert.Equal(0, copy.Move.In.Zoom);
        Assert.Equal(0, copy.Move.In.Curve);
        Assert.Equal(3, copy.Move.In.Easing);
        Assert.Equal(2, copy.Move.In.Function);
        Assert.NotSame(source.Move, copy.Move);
        Assert.Equal(copy, JsonSerializer.Deserialize<TransitionOptions>(JsonSerializer.Serialize(copy)));
    }

    [Fact]
    public void Legacy_commands_keep_the_native_move_defaults()
    {
        var command = ObsMoveProtocol.DecodeCommand("{\"outgoingPath\":\"before.png\",\"incomingPath\":\"after.png\",\"durationMilliseconds\":300}"u8);
        Assert.Equal(new MoveOptions(), command.Move ?? new MoveOptions());
    }
}
