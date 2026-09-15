using StartupProfiles.App.Maintenance;
using StartupProfiles.Core.Startup;

namespace StartupProfiles.App.Tests;

public sealed class MaintenanceCommandsTests
{
    [Theory]
    [InlineData("--register-login", MaintenanceCommand.RegisterLogin)]
    [InlineData("--unregister-login", MaintenanceCommand.UnregisterLogin)]
    [InlineData("--register-protocol", MaintenanceCommand.RegisterProtocol)]
    [InlineData("--unregister-protocol", MaintenanceCommand.UnregisterProtocol)]
    [InlineData("--list-startup", MaintenanceCommand.ListStartup)]
    [InlineData("--take-over-startup", MaintenanceCommand.TakeOverStartup)]
    [InlineData("--restore-startup", MaintenanceCommand.RestoreStartup)]
    [InlineData("--REGISTER-LOGIN", MaintenanceCommand.RegisterLogin)]
    public void Parse_MapsKnownFlags(string flag, MaintenanceCommand expected) =>
        Assert.Equal(expected, MaintenanceCommands.Parse([flag]));

    [Fact]
    public void FormatTakeover_ListsWhatMovedAndWhatWasLeftOn()
    {
        var result = new StartupTakeoverResult(
            Adopted: [Entry("Steam"), Entry("Discord")],
            LeftEnabled: [Entry("SecurityHealth")],
            Failed: []);

        var report = MaintenanceCommands.FormatTakeover(result);

        Assert.Contains("Moved 2 startup app(s) into the Everything profile", report);
        Assert.Contains("  Steam", report);
        Assert.Contains("Left on", report);
        Assert.Contains("  SecurityHealth", report);
        Assert.DoesNotContain("could not switch off", report);
    }

    [Fact]
    public void FormatTakeover_SaysSo_WhenNothingToTakeOver() =>
        Assert.Equal("No other startup apps to take over.",
            MaintenanceCommands.FormatTakeover(new StartupTakeoverResult([], [], [])));

    private static StartupEntry Entry(string name) =>
        new() { Source = StartupEntrySource.UserRunKey, Key = name, Name = name, IsEnabled = true, CanToggle = true };

    [Theory]
    [InlineData("--headless")]
    [InlineData("register")]
    [InlineData("--port")]
    public void Parse_ReturnsNone_ForNonMaintenanceArgs(string flag) =>
        Assert.Equal(MaintenanceCommand.None, MaintenanceCommands.Parse([flag]));

    [Fact]
    public void Parse_ReturnsNone_ForEmptyArgs() =>
        Assert.Equal(MaintenanceCommand.None, MaintenanceCommands.Parse([]));
}
