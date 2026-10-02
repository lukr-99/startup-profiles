using StartupProfiles.App.Themes;
using StartupProfiles.App.Tray;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Tests;

public sealed class TrayMenuModelTests
{
    private static readonly Profile[] Profiles =
    [
        new() { Id = "work", Name = "Work", Icon = "💼" },
        new() { Id = "chill", Name = "Chill" },
    ];

    [Fact]
    public void OpenLauncher_IsFirst_AndTheDefault()
    {
        var first = TrayMenuModel.Build(Profiles, null, ThemeMode.System, "0.1.0")[0];

        Assert.Equal(TrayCommand.OpenLauncher, first.Command);
        Assert.True(first.IsDefault);
    }

    [Fact]
    public void EveryProfileIsListed_WithItsGlyph_AndTheActiveOneChecked()
    {
        var runs = TrayMenuModel.Build(Profiles, "chill", ThemeMode.System, "0.1.0")
            .Where(e => e.Command == TrayCommand.RunProfile).ToList();

        Assert.Equal(["work", "chill"], runs.Select(r => r.Argument));
        Assert.Equal("💼  Work", runs[0].Header);
        Assert.Equal("C  Chill", runs[1].Header);
        Assert.Equal([false, true], runs.Select(r => r.IsChecked));
    }

    [Fact]
    public void WithNoProfiles_ThereIsNoRunSection()
    {
        var menu = TrayMenuModel.Build([], null, ThemeMode.System, "0.1.0");

        Assert.DoesNotContain(menu, e => e.Header == "Run a profile");
        Assert.False(menu[1].IsSeparator && menu[2].IsSeparator);
    }

    [Theory]
    [InlineData(ThemeMode.System, "Same as Windows")]
    [InlineData(ThemeMode.Light, "Light")]
    [InlineData(ThemeMode.Dark, "Dark")]
    public void TheThemeSubmenu_ChecksTheCurrentMode(ThemeMode mode, string expected)
    {
        var theme = TrayMenuModel.Build(Profiles, null, mode, "0.1.0").Single(e => e.Header == "Theme");

        Assert.Equal(expected, theme.Children.Single(c => c.IsChecked == true).Header);
        Assert.All(theme.Children, c => Assert.Equal(TrayCommand.SetTheme, c.Command));
        Assert.Equal(mode, ThemeManager.Parse(theme.Children.Single(c => c.IsChecked == true).Argument));
    }

    [Fact]
    public void AnAvailableUpdate_ReplacesTheCheckItem()
    {
        var menu = TrayMenuModel.Build(Profiles, null, ThemeMode.System, "0.1.0", availableUpdate: "0.2.0");

        Assert.Contains(menu, e => e is { Command: TrayCommand.InstallUpdate, Header: "Install version 0.2.0" });
        Assert.DoesNotContain(menu, e => e.Command == TrayCommand.CheckForUpdates);
    }

    [Fact]
    public void WhileChecking_TheCheckItemIsGreyedOut()
    {
        var check = TrayMenuModel.Build(Profiles, null, ThemeMode.System, "0.1.0", checkingForUpdates: true)
            .Single(e => e.Command == TrayCommand.CheckForUpdates);

        Assert.False(check.IsEnabled);
        Assert.Equal("Checking for updates...", check.Header);
    }

    [Fact]
    public void Exit_IsLast_AfterTheVersion()
    {
        var menu = TrayMenuModel.Build(Profiles, null, ThemeMode.System, "1.2.3");

        Assert.Equal(TrayCommand.Exit, menu[^1].Command);
        Assert.Contains(menu, e => e.Header == "Version 1.2.3" && e.Command == TrayCommand.None);
    }

    [Theory]
    [InlineData(null, null, "Startup Profiles")]
    [InlineData("Work", null, "Startup Profiles · Work")]
    public void TheToolTip_NamesTheActiveProfile(string? active, string? update, string expected)
    {
        Assert.Equal(expected, TrayMenuModel.ToolTip(active, update));
    }

    [Fact]
    public void TheToolTip_MentionsAWaitingUpdate()
    {
        Assert.EndsWith("Update 0.2.0 available", TrayMenuModel.ToolTip(null, "0.2.0"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false, "Light")]
    [InlineData(false, true, "Dark")]
    [InlineData(true, true, "HighContrast")]
    [InlineData(true, false, "HighContrast")]
    public void ThemeTokens_FollowContrastFirst_ThenDark(bool highContrast, bool isDark, string expected)
    {
        Assert.Equal(expected, ThemeManager.TokensFor(highContrast, isDark));
    }
}
