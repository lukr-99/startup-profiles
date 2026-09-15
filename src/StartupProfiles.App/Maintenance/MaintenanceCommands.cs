using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Storage;
using StartupProfiles.Core.Windows;
using StartupProfiles.Windows;

namespace StartupProfiles.App.Maintenance;

/// <summary>
/// The install/uninstall maintenance commands the installer invokes on the app itself
/// (<c>StartupProfiles.exe --register-login</c> and friends). Each runs the matching Windows adapter
/// against the current executable path and exits - no window, no API host, no single-instance mutex.
/// Registering at login and the protocol here (rather than from PowerShell) keeps the key format owned
/// by the app's seams (<see cref="IStartupRegistration"/> / <see cref="IProtocolRegistration"/>).
/// The startup commands print a report to stdout for the installer to show.
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
            "--list-startup" => MaintenanceCommand.ListStartup,
            "--take-over-startup" => MaintenanceCommand.TakeOverStartup,
            "--restore-startup" => MaintenanceCommand.RestoreStartup,
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

    /// <summary>One line per entry: on/off, source, name, and what a profile would launch.</summary>
    public static string FormatEntries(IEnumerable<StartupEntry> entries) =>
        string.Join(Environment.NewLine, entries.Select(e =>
            $"  [{(e.IsEnabled ? "on " : "off")}] {e.Source,-19} {e.Name}" +
            (e.CanToggle ? "" : " (all users - needs admin)") +
            (e.Launch is { } launch ? $"  ->  {launch.Target} {launch.Arguments}".TrimEnd() : "  ->  (cannot launch)")));

    public static string FormatTakeover(StartupTakeoverResult result)
    {
        var lines = new List<string>
        {
            result.Adopted.Count == 0
                ? "No other startup apps to take over."
                : $"Moved {result.Adopted.Count} startup app(s) into the Everything profile and switched them off in Windows:",
        };
        lines.AddRange(result.Adopted.Select(e => $"  {e.Name}"));

        if (result.Failed.Count > 0)
        {
            lines.Add("Added to Everything but could not switch off (they still start with Windows):");
            lines.AddRange(result.Failed.Select(e => $"  {e.Name}"));
        }

        if (result.LeftEnabled.Count > 0)
        {
            lines.Add("Left on (registered for all users, policy-managed, or not launchable from a profile):");
            lines.AddRange(result.LeftEnabled.Select(e => $"  {e.Name}"));
        }

        return string.Join(Environment.NewLine, lines);
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
            case MaintenanceCommand.ListStartup:
                Console.WriteLine(FormatEntries(new WindowsStartupAppCatalog().GetEntries()));
                break;
            case MaintenanceCommand.TakeOverStartup:
                Console.WriteLine(FormatTakeover(CreateTakeover().TakeOver()));
                break;
            case MaintenanceCommand.RestoreStartup:
                var restored = CreateTakeover().Restore();
                Console.WriteLine(restored.Count == 0
                    ? "No startup apps to switch back on."
                    : $"Switched {restored.Count} startup app(s) back on: {string.Join(", ", restored.Select(e => e.Name))}");
                break;
        }
    }

    private static StartupTakeover CreateTakeover() => new(
        new WindowsStartupAppCatalog(),
        new ProfileStore(),
        new StartupTakeoverStore(),
        WindowsStartupRegistration.DefaultValueName);
}
