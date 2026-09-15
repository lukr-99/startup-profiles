using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;

namespace StartupProfiles.Core.Tests.Startup;

public sealed class StartupCommandLineTests
{
    [Theory]
    [InlineData("\"C:\\Program Files (x86)\\Steam\\steam.exe\" -silent", @"C:\Program Files (x86)\Steam\steam.exe", "-silent")]
    [InlineData("\"C:\\Program Files\\Adobe\\AdobeCollabSync.exe\"", @"C:\Program Files\Adobe\AdobeCollabSync.exe", null)]
    [InlineData(@"C:\Program Files\UniFi Identity Standard\Identity.exe /autorun", @"C:\Program Files\UniFi Identity Standard\Identity.exe", "/autorun")]
    [InlineData(@"C:\Program Files\Docker\Docker\Docker Desktop.exe", @"C:\Program Files\Docker\Docker\Docker Desktop.exe", null)]
    [InlineData(@"C:\Users\me\AppData\Roaming\Spotify\Spotify.exe --autostart --minimized", @"C:\Users\me\AppData\Roaming\Spotify\Spotify.exe", "--autostart --minimized")]
    [InlineData(@"C:\Apps\launcher.exe ", @"C:\Apps\launcher.exe", null)]
    [InlineData(@"C:\Tools\my.exec.tool\run.exe --x", @"C:\Tools\my.exec.tool\run.exe", "--x")]
    [InlineData("rundll32 shell32.dll,Control_RunDLL", "rundll32", "shell32.dll,Control_RunDLL")]
    public void ToLaunchAction_SplitsTargetAndArguments(string commandLine, string target, string? arguments)
    {
        var action = StartupCommandLine.ToLaunchAction(commandLine);

        Assert.NotNull(action);
        Assert.Equal(ActionType.LaunchApp, action!.Type);
        Assert.Equal(target, action.Target);
        Assert.Equal(arguments, action.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void ToLaunchAction_ReturnsNull_ForBlankCommand(string? commandLine) =>
        Assert.Null(StartupCommandLine.ToLaunchAction(commandLine));
}
