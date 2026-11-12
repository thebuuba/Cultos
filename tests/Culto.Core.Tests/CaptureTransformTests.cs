using Culto.Core;
using Xunit;

public class CaptureTransformTests
{
    private static WindowCaptureOptions Source() => new() { WindowHandle = 1, ProcessId = 1,
        WindowClass = "Test", Executable = "test.exe" };

    [Fact]
    public void AltCropsSourcePixelsAndKeepsTheRemainingImageInPlace()
    {
        var source = Source();
        var crop = CaptureGeometry.Drag(source, 1920, 1080, CaptureHandle.Left, 300, 0, crop: true, snap: false);
        Assert.Equal(300, crop.CropLeft);
        var bounds = CaptureGeometry.Bounds(crop, 1920, 1080);
        Assert.Equal(300, bounds.X, 6); Assert.Equal(1620, bounds.Width, 6);
        var uncrop = CaptureGeometry.Drag(crop, 1920, 1080, CaptureHandle.Left, -100, 0, crop: true, snap: false);
        Assert.Equal(200, uncrop.CropLeft);
        Assert.Equal(200, CaptureGeometry.Bounds(uncrop, 1920, 1080).X, 6);
        Assert.Equal(0, source.CropLeft);
    }

    [Fact]
    public void CroppingAccountsForDisplayScaleAndCannotRemoveTheWholeSource()
    {
        var source = Source() with { Transform = new(0, 0, .5, .5) };
        var crop = CaptureGeometry.Drag(source, 1920, 1080, CaptureHandle.TopLeft, 50, 25, crop: true, snap: false);
        Assert.Equal(100, crop.CropLeft); Assert.Equal(50, crop.CropTop);
        var all = CaptureGeometry.Drag(source, 1920, 1080, CaptureHandle.Right, -9000, 0, crop: true, snap: false);
        Assert.Equal(1919, all.CropRight);
        Assert.True(CaptureGeometry.Bounds(all, 1920, 1080).Width > 0);
    }

    [Fact]
    public void NormalResizeKeepsRatioAndShiftAllowsIndependentDimensions()
    {
        var normal = CaptureGeometry.Drag(Source(), 1920, 1080, CaptureHandle.BottomRight, -960, -100, snap: false);
        var box = CaptureGeometry.Bounds(normal, 1920, 1080);
        Assert.Equal(1920d / 1080, box.Width / box.Height, 6);
        var stretched = CaptureGeometry.Drag(Source(), 1920, 1080, CaptureHandle.BottomRight, -960, -100, stretch: true, snap: false);
        box = CaptureGeometry.Bounds(stretched, 1920, 1080);
        Assert.Equal(960, box.Width, 6); Assert.Equal(980, box.Height, 6);
    }

    [Fact]
    public void CtrlDisablesEdgeAndCenterSnapping()
    {
        var source = Source() with { Transform = new(.1, .1, .5, .5) };
        var snap = CaptureGeometry.Drag(source, 1920, 1080, CaptureHandle.Move, -186, -102);
        var free = CaptureGeometry.Drag(source, 1920, 1080, CaptureHandle.Move, -186, -102, snap: false);
        Assert.Equal(0, CaptureGeometry.Bounds(snap, 1920, 1080).X, 6);
        Assert.Equal(6, CaptureGeometry.Bounds(free, 1920, 1080).X, 6);
        Assert.Equal(6, CaptureGeometry.Bounds(free, 1920, 1080).Y, 6);
    }

    [Fact]
    public void FitAfterCroppingAndResetHaveDifferentIntentAndPersistThroughProtocol()
    {
        var crop = Source() with { CropLeft = 300, CropRight = 300 };
        var fit = CaptureGeometry.Fit(crop, 1920, 1080);
        Assert.Equal(300, fit.CropLeft);
        var box = CaptureGeometry.Bounds(fit, 1920, 1080);
        Assert.Equal(1080, box.Height, 6); Assert.Equal(300, box.X, 6);
        var reset = CaptureGeometry.Reset(crop);
        Assert.Equal(0, reset.CropLeft); Assert.Null(reset.Transform);
        Assert.Equal(fit, ObsCaptureCommand.Decode(ObsCaptureCommand.Encode(new(fit))).Source);
        Assert.Throws<InvalidDataException>(() => (fit with { Transform = new(double.NaN, 0, 1, 1) }).ValidateTarget());
    }
}
