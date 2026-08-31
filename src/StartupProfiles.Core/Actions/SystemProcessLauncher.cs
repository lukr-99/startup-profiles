using System.ComponentModel;
using System.Diagnostics;

namespace StartupProfiles.Core.Actions;

/// <summary>Default <see cref="IProcessLauncher"/> backed by <see cref="Process"/>.</summary>
public sealed class SystemProcessLauncher : IProcessLauncher
{
    public ActionResult Start(ProcessLaunchSpec spec)
    {
        var psi = new ProcessStartInfo
        {
            FileName = spec.FileName,
            UseShellExecute = spec.UseShellExecute,
            CreateNoWindow = spec.CreateNoWindow,
        };
        if (!string.IsNullOrEmpty(spec.Arguments)) psi.Arguments = spec.Arguments;
        if (!string.IsNullOrEmpty(spec.Verb)) psi.Verb = spec.Verb;

        try
        {
            using var process = Process.Start(psi);
            return process is null
                ? ActionResult.Fail($"Failed to start '{spec.FileName}'.")
                : ActionResult.Ok($"Started '{spec.FileName}' (pid {process.Id}).");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return ActionResult.Fail($"Failed to start '{spec.FileName}': {ex.Message}");
        }
    }

    public ActionResult KillByName(string processName)
    {
        var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        var processes = Process.GetProcessesByName(name);
        if (processes.Length == 0) return ActionResult.Ok($"No running process named '{name}'.");

        var killed = 0;
        foreach (var process in processes)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                killed++;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // Process already exited or cannot be accessed; count only the ones we killed.
            }
            finally
            {
                process.Dispose();
            }
        }

        return ActionResult.Ok($"Killed {killed} process(es) named '{name}'.");
    }
}
