using StartupProfiles.Core.Models;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Core.Actions;

/// <summary>Connects the VPN entry named by the action's target.</summary>
public sealed class StartVpnHandler : IActionHandler
{
    private readonly IVpnConnector _vpn;

    public StartVpnHandler(IVpnConnector vpn) => _vpn = vpn;

    public ActionType Type => ActionType.StartVpn;

    public Task<ActionResult> ExecuteAsync(ProfileAction action, CancellationToken ct = default)
        => Task.FromResult(_vpn.Connect(action.Target));
}
