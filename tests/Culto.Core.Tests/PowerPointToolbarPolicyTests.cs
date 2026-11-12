using Culto.Desktop.Services;
using Xunit;

namespace Culto.Core.Tests;

public sealed class PowerPointToolbarPolicyTests
{
    private static PowerPointWindowSnapshot Window(int handle, int owner = 0, string type = "MsoCommandBar",
        uint process = 41, bool visible = true) => new(handle, owner, process, 7, type, "Synthetic", visible);
    private static Dictionary<nint, PowerPointWindowSnapshot> Windows(params PowerPointWindowSnapshot[] values) =>
        values.ToDictionary(value => value.Handle);
    private static PowerPointWindowSnapshot Show() => Window(1, type: "PPTFrameClass");

    [Fact]
    public void AcceptsOnlyToolbarWhoseOwnerChainReachesOurShow()
    {
        Assert.True(PowerPointToolbarPolicy.CanHide(1, 41, 2, Windows(Show(), Window(2, 1))));
        Assert.True(PowerPointToolbarPolicy.CanHide(1, 41, 3,
            Windows(Show(), Window(2, 1, "OwnedPopup"), Window(3, 2))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void RejectsUnownedOrMissingOwnerEvenInSameProcess(int owner)
    {
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2, Windows(Show(), Window(2, owner))));
    }

    [Fact]
    public void RejectsAnotherPresentationOrSharedPowerPointApplicationWindow()
    {
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2,
            Windows(Show(), Window(2, 9), Window(9, type: "PPTFrameClass"))));
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2,
            Windows(Show(), Window(2, 9), Window(9, type: "PPTApplication"))));
    }

    [Theory]
    [InlineData("MsoCommandBarDock")]
    [InlineData("MsoCommandBarPopup")]
    [InlineData("PPTFrameClass")]
    [InlineData("")]
    public void RejectsEveryUnrecognizedClassRegardlessOfTitleOrOwner(string type)
    {
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2, Windows(Show(), Window(2, 1, type))));
    }

    [Fact]
    public void RejectsWrongProcessAnywhereAlongOwnershipChain()
    {
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2,
            Windows(Show(), Window(2, 1, process: 42))));
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 3,
            Windows(Show(), Window(2, 1, "OwnedPopup", process: 42), Window(3, 2))));
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 42, 2, Windows(Show(), Window(2, 1))));
    }

    [Fact]
    public void RejectsCyclesWithoutLooping()
    {
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2, Windows(Show(), Window(2, 2))));
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2, Windows(Show(), Window(2, 3), Window(3, 2))));
    }

    [Fact]
    public void RejectsUnknownOrHiddenTargetsAndInvalidShowIdentity()
    {
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2, Windows(Show(), Window(2, 1, visible: false))));
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 1, Windows(Show())));
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2, Windows(Show())));
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2, Windows(Window(2, 1))));
        Assert.False(PowerPointToolbarPolicy.CanHide(0, 41, 2, Windows(Show(), Window(2, 1))));
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 0, 2, Windows(Show(), Window(2, 1))));
    }

    [Fact]
    public void RejectsSnapshotHandleMismatchAndOwnershipThatChangedSinceFirstObservation()
    {
        var windows = Windows(Show(), Window(2, 1));
        Assert.True(PowerPointToolbarPolicy.CanHide(1, 41, 2, windows));
        windows[2] = Window(2, 9);
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2, windows));
        windows[2] = Window(3, 1);
        Assert.False(PowerPointToolbarPolicy.CanHide(1, 41, 2, windows));
    }
}
