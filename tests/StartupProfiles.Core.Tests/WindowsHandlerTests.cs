using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Core.Tests;

public sealed class WindowsHandlerTests
{
    private sealed class FakeServiceController : IServiceController
    {
        public List<string> Started { get; } = [];

        public ActionResult StartService(string serviceName)
        {
            Started.Add(serviceName);
            return ActionResult.Ok($"started {serviceName}");
        }
    }

    private sealed class FakeVpnConnector : IVpnConnector
    {
        public List<string> Connected { get; } = [];

        public ActionResult Connect(string connectionName)
        {
            Connected.Add(connectionName);
            return ActionResult.Ok($"connected {connectionName}");
        }
    }

    [Fact]
    public async Task StartServiceHandler_DelegatesToController()
    {
        var services = new FakeServiceController();
        var handler = new StartServiceHandler(services);

        var result = await handler.ExecuteAsync(new ProfileAction { Type = ActionType.StartService, Target = "Spooler" });

        Assert.True(result.Success);
        Assert.Equal("Spooler", Assert.Single(services.Started));
    }

    [Fact]
    public async Task StartVpnHandler_DelegatesToConnector()
    {
        var vpn = new FakeVpnConnector();
        var handler = new StartVpnHandler(vpn);

        await handler.ExecuteAsync(new ProfileAction { Type = ActionType.StartVpn, Target = "Work VPN" });

        Assert.Equal("Work VPN", Assert.Single(vpn.Connected));
    }

    [Fact]
    public async Task CreateDefault_WithAdapters_RegistersServiceAndVpnHandlers()
    {
        var registry = ActionHandlerRegistry.CreateDefault(
            new FakeProcessLauncher(),
            new FakeServiceController(),
            new FakeVpnConnector());
        var runner = new ProfileRunner(registry, new RecordingDelayer(), TimeProvider.System);

        var run = await runner.RunAsync(new Profile
        {
            Id = "p",
            Name = "Test",
            Actions =
            [
                new ProfileAction { Type = ActionType.StartService, Target = "Spooler" },
                new ProfileAction { Type = ActionType.StartVpn, Target = "Work VPN" },
            ],
        });

        Assert.True(run.Succeeded);
    }
}
