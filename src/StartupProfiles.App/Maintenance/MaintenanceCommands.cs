using StartupProfiles.Core.Windows;
using StartupProfiles.Windows;

namespace StartupProfiles.App.Maintenance;

/// <summary>
/// The install/uninstall maintenance commands the installer invokes on the app itself
/// (<c>StartupProfiles.exe --register-login</c> and friends). Each runs the matching Windows adapter
/// against the current executable path and exits - no window, no API host, no single-instance mutex.
/// Registering at login and the protocol here (rather than from PowerShell) keeps the key format owned
/// by the app's seams (<see cref="IStartupRegistration"/> / <see cref="IProtocolRegistration"/>).
/// </summary>
public static class MaintenanceCommands
{
    public static MaintenanceCommand Parse(IReadOnlyList<string> args) =>
        args.Count > 0 ? args[0].ToLowerInvariant() switch
        {
            "--register-login" => MaintenanceCommand.RegisterLogin,
            "--unregister-login" => MaintenanceCommand.UnregisterLogin,
            "--register-protocol" => MaintenanceCommand.RegisterProtocol,
            "--unregister-protocol" => MaintenanceCommand.UnregisterProtocol,
            _ => MaintenanceCommand.None,
        } : MaintenanceCommand.None;

    /// <summary>Runs a maintenance command if the args name one; returns true if it handled them.</summary>
    public static bool TryRun(IReadOnlyList<string> args)
    {
        var command = Parse(args);
        if (command == MaintenanceCommand.None) return false;

        Run(command, Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve the executable path."));
        return true;
    }

    private static void Run(MaintenanceCommand command, string executablePath)
    {
        var login = new WindowsStartupRegistration();
        var protocol = new WindowsProtocolRegistration();

        switch (command)
        {
            case MaintenanceCommand.RegisterLogin:
                login.Enable($"\"{executablePath}\"");
                break;
            case MaintenanceCommand.UnregisterLogin:
                login.Disable();
                break;
            case MaintenanceCommand.RegisterProtocol:
                protocol.Register(executablePath);
                break;
            case MaintenanceCommand.UnregisterProtocol:
                protocol.Unregister();
                break;
        }
    }
}
