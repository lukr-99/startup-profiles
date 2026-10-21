using StartupProfiles.App.Launcher;

namespace StartupProfiles.App.Tests;

public sealed class LauncherLayoutTests
{
    private const double Wide = 1920;

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(6, 6)]
    [InlineData(7, 7)]
    public void Every_tile_gets_its_own_column_while_the_row_fits(int tiles, int columns) =>
        Assert.Equal(columns, LauncherLayout.Columns(tiles, Wide));

    [Fact]
    public void Tiles_wrap_into_balanced_rows_on_a_narrow_work_area()
    {
        // 800 wide fits 4 per row; 7 tiles then take 2 rows of 4 and 3 -> balanced to 4 columns.
        Assert.Equal(4, LauncherLayout.Columns(7, 800));
        // 9 tiles: 3 rows at 4 max -> 3 columns each.
        Assert.Equal(3, LauncherLayout.Columns(9, 800));
    }

    [Fact]
    public void A_tiny_work_area_still_gets_one_column() =>
        Assert.Equal(1, LauncherLayout.Columns(5, 100));

    [Fact]
    public void Window_sits_at_the_bottom_centre_of_the_work_area()
    {
        var (left, top) = LauncherLayout.Position(1000, 400, 0, 0, 1920, 1040);
        Assert.Equal(460, left);
        Assert.Equal(1040 - 400 - LauncherLayout.ScreenMargin, top);
    }

    [Fact]
    public void Position_respects_a_work_area_that_does_not_start_at_the_origin()
    {
        var (left, top) = LauncherLayout.Position(1000, 400, 1920, 100, 1920, 1000);
        Assert.Equal(1920 + 460, left);
        Assert.Equal(100 + 1000 - 400 - LauncherLayout.ScreenMargin, top);
    }

    [Fact]
    public void An_oversized_window_keeps_its_top_left_on_screen()
    {
        var (left, top) = LauncherLayout.Position(2000, 1200, 0, 0, 1920, 1040);
        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }
}
