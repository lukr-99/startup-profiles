namespace StartupProfiles.Core.Actions;

/// <summary>
/// The process-launch seam. Handlers never touch <c>System.Diagnostics.Process</c> directly so tests
/// can inject a deterministic fake (rule: isolate process launching behind a controllable seam).
/// </summary>
public interface IProcessLauncher
{
    ActionResult Start(ProcessLaunchSpec spec);
    ActionResult KillByName(string processName);
}
