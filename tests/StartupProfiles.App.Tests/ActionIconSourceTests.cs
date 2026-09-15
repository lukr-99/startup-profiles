using StartupProfiles.App.Ui;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Tests;

public sealed class ActionIconSourceTests
{
    [Theory]
    [InlineData(ActionType.LaunchApp, @"C:\Program Files (x86)\Steam\steam.exe", "-silent", @"C:\Program Files (x86)\Steam\steam.exe")]
    [InlineData(ActionType.LaunchApp, "\"C:\\Apps\\app.exe\"", null, @"C:\Apps\app.exe")]
    [InlineData(ActionType.LaunchApp, "code.exe", null, "code.exe")]
    [InlineData(ActionType.OpenFile, @"C:\Users\me\Startup\Relay.lnk", null, @"C:\Users\me\Startup\Relay.lnk")]
    [InlineData(ActionType.OpenFolder, @"C:\Projects", null, @"C:\Projects")]
    [InlineData(ActionType.RunScript, @"C:\Scripts\setup.ps1", null, @"C:\Scripts\setup.ps1")]
    public void For_UsesTheTargetPath(ActionType type, string target, string? arguments, string expected) =>
        Assert.Equal(expected, ActionIconSource.For(type, target, arguments));

    [Fact]
    public void For_PackagedApp_UsesTheAppNotExplorer() =>
        Assert.Equal(
            @"shell:AppsFolder\OpenAI.ChatGPT-Desktop_2p2nqsd0c76g0!ChatGPT",
            ActionIconSource.For(ActionType.LaunchApp, "explorer.exe", @"shell:AppsFolder\OpenAI.ChatGPT-Desktop_2p2nqsd0c76g0!ChatGPT"));

    [Theory]
    [InlineData("--processStart Discord.exe")]
    [InlineData("--processStart \"Discord.exe\" --process-start-args \"--start-minimized\"")]
    public void For_SquirrelUpdater_UsesTheAppItStarts(string arguments) =>
        Assert.Equal(
            @"C:\Users\me\AppData\Local\Discord\Discord.exe",
            ActionIconSource.For(ActionType.LaunchApp, @"C:\Users\me\AppData\Local\Discord\Update.exe", arguments));

    [Fact]
    public void For_PlainExplorerLaunch_UsesExplorer() =>
        Assert.Equal("explorer.exe", ActionIconSource.For(ActionType.LaunchApp, "explorer.exe", @"C:\Projects"));

    [Fact]
    public void For_Url_UsesTheInternetShortcutIcon() =>
        Assert.Equal(ActionIconSource.UrlIcon, ActionIconSource.For(ActionType.OpenUrl, "https://example.com", null));

    [Theory]
    [InlineData(ActionType.Delay, "", null)]
    [InlineData(ActionType.KillProcess, "Teams.exe", null)]
    [InlineData(ActionType.StartService, "Spooler", null)]
    [InlineData(ActionType.RunScript, "exit 0", null)]
    [InlineData(ActionType.LaunchApp, "  ", null)]
    public void For_ReturnsNull_WhenThereIsNoMeaningfulIcon(ActionType type, string target, string? arguments) =>
        Assert.Null(ActionIconSource.For(type, target, arguments));
}
