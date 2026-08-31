using System.Diagnostics;
using System.Text;
using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Windows;

/// <summary>
/// Connects a VPN entry via the built-in <c>rasdial</c> command using the entry's saved credentials.
/// Credentials are never passed on the command line - the connection must already be saved in Windows.
/// </summary>
public sealed class WindowsVpnConnector : IVpnConnector
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60);

    public ActionResult Connect(string connectionName)
    {
        var psi = new ProcessStartInfo("rasdial.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add(connectionName);

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return ActionResult.Fail($"Failed to start rasdial for '{connectionName}'.");

            if (!process.WaitForExit(ConnectTimeout))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return ActionResult.Fail($"VPN '{connectionName}' did not connect within {ConnectTimeout.TotalSeconds:0}s.");
            }

            if (process.ExitCode == 0) return ActionResult.Ok($"Connected VPN '{connectionName}'.");

            var error = process.StandardError.ReadToEnd().Trim();
            var output = process.StandardOutput.ReadToEnd().Trim();
            var detail = string.IsNullOrEmpty(error) ? output : error;
            return ActionResult.Fail($"VPN '{connectionName}' failed (rasdial {process.ExitCode}): {detail}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return ActionResult.Fail($"Failed to start rasdial for '{connectionName}': {ex.Message}");
        }
    }
}
