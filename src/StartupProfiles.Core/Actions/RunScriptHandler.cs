using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Actions;

/// <summary>Runs a script or shell command via <c>cmd.exe /c</c> (mirrors Relay's ScriptProvider).</summary>
public sealed class RunScriptHandler : IActionHandler
{
    private readonly IProcessLauncher _launcher;

    public RunScriptHandler(IProcessLauncher launcher) => _launcher = launcher;

    public ActionType Type => ActionType.RunScript;

    public Task<ActionResult> ExecuteAsync(ProfileAction action, CancellationToken ct = default)
    {
        var command = string.IsNullOrEmpty(action.Arguments)
            ? action.Target
            : $"{action.Target} {action.Arguments}";

        var spec = new ProcessLaunchSpec
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        return Task.FromResult(_launcher.Start(spec));
    }
}
