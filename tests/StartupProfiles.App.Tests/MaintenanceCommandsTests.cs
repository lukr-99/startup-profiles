using StartupProfiles.App.Maintenance;

namespace StartupProfiles.App.Tests;

public sealed class MaintenanceCommandsTests
{
    [Theory]
    [InlineData("--register-login", MaintenanceCommand.RegisterLogin)]
    [InlineData("--unregister-login", MaintenanceCommand.UnregisterLogin)]
    [InlineData("--register-protocol", MaintenanceCommand.RegisterProtocol)]
    [InlineData("--unregister-protocol", MaintenanceCommand.UnregisterProtocol)]
    [InlineData("--REGISTER-LOGIN", MaintenanceCommand.RegisterLogin)]
    public void Parse_MapsKnownFlags(string flag, MaintenanceCommand expected) =>
        Assert.Equal(expected, MaintenanceCommands.Parse([flag]));

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
