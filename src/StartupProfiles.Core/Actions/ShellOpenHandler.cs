using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Actions;

/// <summary>
/// Opens a URL, file, or folder through the shell (UseShellExecute lets Windows pick the default
/// handler). One instance is registered per opened <see cref="ActionType"/>.
/// </summary>
public sealed class ShellOpenHandler : IActionHandler
{
    private readonly IProcessLauncher _launcher;

    public ShellOpenHandler(ActionType type, IProcessLauncher launcher)
    {
        Type = type;
        _launcher = launcher;
    }

    public ActionType Type { get; }

    public Task<ActionResult> ExecuteAsync(ProfileAction action, CancellationToken ct = default)
    {
        var spec = new ProcessLaunchSpec { FileName = action.Target, UseShellExecute = true };
        return Task.FromResult(_launcher.Start(spec));
    }
}
