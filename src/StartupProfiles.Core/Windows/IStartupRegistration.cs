namespace StartupProfiles.Core.Windows;

/// <summary>
/// Registers (or unregisters) the launcher to run at login. The seam is portable; the Windows adapter
/// lives in StartupProfiles.Windows so Core stays platform-independent and testable.
/// </summary>
public interface IStartupRegistration
{
    /// <summary>True if the launcher is currently set to run at login.</summary>
    bool IsEnabled();

    /// <summary>Registers <paramref name="commandLine"/> to run at login (idempotent).</summary>
    void Enable(string commandLine);

    /// <summary>Removes the login registration if present (idempotent).</summary>
    void Disable();
}
