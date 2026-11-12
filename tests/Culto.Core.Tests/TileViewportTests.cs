using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class TileViewportTests
{
    [Fact]
    public void LargeFolderRealizesOnlyVisibleRowsAndSmallOverscan()
    {
        var view = TileViewport.Calculate(10000, 760, 300, 184, 148, 1480);
        Assert.Equal(4, view.Columns);
        Assert.Equal(190, view.CellWidth);
        Assert.Equal(370000, view.ExtentHeight);
        Assert.Equal(36, view.FirstIndex);
        Assert.Equal(56, view.EndIndex);
    }

    [Fact]
    public void NarrowPaneKeepsOneColumnInsideItsWidth()
    {
        var view = TileViewport.Calculate(20, 120, 300, 184, 148, 0);
        Assert.Equal(1, view.Columns);
        Assert.Equal(120, view.CellWidth);
        Assert.Equal(0, view.FirstIndex);
        Assert.Equal(4, view.EndIndex);
    }

    [Fact]
    public void RemovingItemsClampsTheOldScrollPosition()
    {
        var view = TileViewport.Calculate(5, 400, 300, 184, 148, 10000);
        Assert.Equal(144, view.Offset);
        Assert.InRange(view.FirstIndex, 0, 4);
        Assert.Equal(5, view.EndIndex);
    }

    [Fact]
    public void EmptyFolderHasNoRealizedItemsOrScrollOffset()
    {
        var view = TileViewport.Calculate(0, 400, 300, 184, 148, 10000);
        Assert.Equal(0, view.ExtentHeight);
        Assert.Equal(0, view.Offset);
        Assert.Equal(0, view.FirstIndex);
        Assert.Equal(0, view.EndIndex);
    }

    [Fact]
    public void ScrollIntoViewMovesOnlyAsFarAsNeeded()
    {
        var view = TileViewport.Calculate(100, 400, 300, 184, 148, 296);
        Assert.Equal(296, view.OffsetForIndex(5, 100));
        Assert.Equal(0, view.OffsetForIndex(0, 100));
        Assert.Equal(588, view.OffsetForIndex(10, 100));
    }

    [Fact]
    public void ResizingChangesColumnsAndRecalculatesBounds()
    {
        var wide = TileViewport.Calculate(100, 800, 300, 184, 148, 1000);
        var narrow = TileViewport.Calculate(100, 400, 300, 184, 148, wide.Offset);
        Assert.Equal(4, wide.Columns);
        Assert.Equal(2, narrow.Columns);
        Assert.Equal(wide.ExtentHeight * 2, narrow.ExtentHeight);
        Assert.InRange(narrow.EndIndex - narrow.FirstIndex, 1, 10);
    }
}
