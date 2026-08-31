using StartupProfiles.Core.Models;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Core.Actions;

/// <summary>
/// Holds the action handlers keyed by <see cref="ActionType"/> (mirrors Relay's ProviderRegistry).
/// The portable handlers are always registered; the Windows-only <see cref="ActionType.StartService"/>
/// and <see cref="ActionType.StartVpn"/> handlers are added only when a platform adapter is supplied,
/// so a portable host reports a clear "no handler" failure instead of throwing.
/// </summary>
public sealed class ActionHandlerRegistry
{
    private readonly Dictionary<ActionType, IActionHandler> _handlers = new();

    public void Register(IActionHandler handler) => _handlers[handler.Type] = handler;

    public bool TryGet(ActionType type, out IActionHandler handler) => _handlers.TryGetValue(type, out handler!);

    /// <summary>
    /// Wires the portable handlers to a process launcher, plus the Windows service/VPN handlers when
    /// their adapters are supplied. Called from the composition root.
    /// </summary>
    public static ActionHandlerRegistry CreateDefault(
        IProcessLauncher launcher,
        IServiceController? services = null,
        IVpnConnector? vpn = null)
    {
        var registry = new ActionHandlerRegistry();
        registry.Register(new LaunchAppHandler(launcher));
        registry.Register(new ShellOpenHandler(ActionType.OpenUrl, launcher));
        registry.Register(new ShellOpenHandler(ActionType.OpenFile, launcher));
        registry.Register(new ShellOpenHandler(ActionType.OpenFolder, launcher));
        registry.Register(new RunScriptHandler(launcher));
        registry.Register(new KillProcessHandler(launcher));
        registry.Register(new DelayHandler());

        if (services is not null) registry.Register(new StartServiceHandler(services));
        if (vpn is not null) registry.Register(new StartVpnHandler(vpn));

        return registry;
    }
}
