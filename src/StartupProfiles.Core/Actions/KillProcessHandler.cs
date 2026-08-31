using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Actions;

/// <summary>Kills every running process whose name matches the action's target.</summary>
public sealed class KillProcessHandler : IActionHandler
{
    private readonly IProcessLauncher _launcher;

    public KillProcessHandler(IProcessLauncher launcher) => _launcher = launcher;

    public ActionType Type => ActionType.KillProcess;

    public Task<ActionResult> ExecuteAsync(ProfileAction action, CancellationToken ct = default)
        => Task.FromResult(_launcher.KillByName(action.Target));
}
