using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Actions;

/// <summary>
/// Holds the action handlers keyed by <see cref="ActionType"/> (mirrors Relay's ProviderRegistry).
/// <see cref="ActionType.StartService"/> and <see cref="ActionType.StartVpn"/> need Windows APIs and
/// are deferred, so they have no handler yet; the runner reports a clear failure if a profile uses one.
/// </summary>
public sealed class ActionHandlerRegistry
{
    private readonly Dictionary<ActionType, IActionHandler> _handlers = new();

    public void Register(IActionHandler handler) => _handlers[handler.Type] = handler;

    public bool TryGet(ActionType type, out IActionHandler handler) => _handlers.TryGetValue(type, out handler!);

    /// <summary>Wires the portable handlers to a process launcher. Called from the composition root.</summary>
    public static ActionHandlerRegistry CreateDefault(IProcessLauncher launcher)
    {
        var registry = new ActionHandlerRegistry();
        registry.Register(new LaunchAppHandler(launcher));
        registry.Register(new ShellOpenHandler(ActionType.OpenUrl, launcher));
        registry.Register(new ShellOpenHandler(ActionType.OpenFile, launcher));
        registry.Register(new ShellOpenHandler(ActionType.OpenFolder, launcher));
        registry.Register(new RunScriptHandler(launcher));
        registry.Register(new KillProcessHandler(launcher));
        registry.Register(new DelayHandler());
        return registry;
    }
}
