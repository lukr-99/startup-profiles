using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Actions;

/// <summary>Launches an application by path, optionally elevated (shell verb "runas") on Windows.</summary>
public sealed class LaunchAppHandler : IActionHandler
{
    private readonly IProcessLauncher _launcher;

    public LaunchAppHandler(IProcessLauncher launcher) => _launcher = launcher;

    public ActionType Type => ActionType.LaunchApp;

    public Task<ActionResult> ExecuteAsync(ProfileAction action, CancellationToken ct = default)
    {
        var spec = new ProcessLaunchSpec
        {
            FileName = action.Target,
            Arguments = action.Arguments,
            // Elevation is only available through the shell verb, which needs UseShellExecute.
            UseShellExecute = action.RunAsAdmin,
            Verb = action.RunAsAdmin ? "runas" : null,
        };
        return Task.FromResult(_launcher.Start(spec));
    }
}
