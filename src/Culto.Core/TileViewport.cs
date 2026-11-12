namespace Culto.Core;

// Geometry only: shared with the WPF gallery and testable without opening a window.
public readonly record struct TileViewport(int Columns, double CellWidth, double CellHeight,
    double Height, double ExtentHeight, double Offset, int FirstIndex, int EndIndex)
{
    public static TileViewport Calculate(int itemCount, double width, double height,
        double preferredWidth, double cellHeight, double offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        if (!double.IsFinite(width) || width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(height) || height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!double.IsFinite(preferredWidth) || preferredWidth <= 0) throw new ArgumentOutOfRangeException(nameof(preferredWidth));
        if (!double.IsFinite(cellHeight) || cellHeight <= 0) throw new ArgumentOutOfRangeException(nameof(cellHeight));
        var columns = (int)Math.Clamp(Math.Floor(width / preferredWidth), 1, int.MaxValue);
        var rows = (itemCount + (long)columns - 1) / columns;
        var extent = rows * cellHeight;
        offset = double.IsNaN(offset) ? 0 : Math.Clamp(offset, 0, Math.Max(0, extent - height));
        // One extra row at each edge prevents blank gaps during wheel movement.
        var firstRow = Math.Max(0, Math.Floor(offset / cellHeight) - 1);
        var endRow = Math.Min(rows, Math.Ceiling((offset + height) / cellHeight) + 1);
        return new(columns, width / columns, cellHeight, height, extent, offset,
            (int)Math.Min(itemCount, firstRow * columns), (int)Math.Min(itemCount, endRow * columns));
    }

    public double OffsetForIndex(int index, int itemCount)
    {
        if (itemCount == 0) return 0;
        index = Math.Clamp(index, 0, itemCount - 1);
        var top = (index / Columns) * CellHeight;
        var desired = top < Offset || CellHeight > Height ? top
            : top + CellHeight > Offset + Height ? top + CellHeight - Height : Offset;
        return Math.Clamp(desired, 0, Math.Max(0, ExtentHeight - Height));
    }
}
