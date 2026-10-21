namespace StartupProfiles.App.Launcher;

/// <summary>
/// Sizes and places the launcher from the profile count. All values are device-independent pixels.
/// One row holds every tile while it fits the work area; past that the tiles wrap into balanced rows.
/// </summary>
public static class LauncherLayout
{
    /// <summary>One tile plus its margin (152 wide, 6 each side).</summary>
    public const double TileSlotWidth = 164;

    /// <summary>Space between the window edge and the tile list (the grid margin, 24 each side).</summary>
    public const double HorizontalChrome = 48;

    /// <summary>Gap kept between the launcher and the screen edges.</summary>
    public const double ScreenMargin = 12;

    /// <summary>Tiles per row for <paramref name="tileCount"/> tiles on a work area <paramref name="workAreaWidth"/> wide.</summary>
    public static int Columns(int tileCount, double workAreaWidth)
    {
        if (tileCount <= 0) return 1;
        var fit = (int)Math.Floor((workAreaWidth - 2 * ScreenMargin - HorizontalChrome) / TileSlotWidth);
        var maxColumns = Math.Max(1, fit);
        if (tileCount <= maxColumns) return tileCount;

        // Too many for one row: use the fewest rows, then spread the tiles evenly across them.
        var rows = (int)Math.Ceiling(tileCount / (double)maxColumns);
        return (int)Math.Ceiling(tileCount / (double)rows);
    }

    /// <summary>Width of the tile list for the given column count.</summary>
    public static double ListWidth(int columns) => Math.Max(1, columns) * TileSlotWidth;

    /// <summary>Top-left of a window of the given size: horizontally centred, resting on the bottom of the work area.</summary>
    public static (double Left, double Top) Position(double windowWidth, double windowHeight, double workLeft, double workTop, double workWidth, double workHeight)
    {
        var left = workLeft + (workWidth - windowWidth) / 2;
        var top = workTop + workHeight - windowHeight - ScreenMargin;

        // A window taller or wider than the work area keeps its top-left corner visible.
        return (Math.Max(workLeft, left), Math.Max(workTop, top));
    }
}
