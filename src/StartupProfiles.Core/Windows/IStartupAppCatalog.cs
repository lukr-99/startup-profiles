using StartupProfiles.Core.Startup;

namespace StartupProfiles.Core.Windows;

/// <summary>
/// Reads the apps the OS starts at login and switches them on or off. The seam is portable; the Windows
/// adapter lives in StartupProfiles.Windows so Core stays platform-independent and testable.
/// </summary>
public interface IStartupAppCatalog
{
    /// <summary>Every registered startup entry, enabled or not.</summary>
    IReadOnlyList<StartupEntry> GetEntries();

    /// <summary>
    /// Switches an entry on or off the way Task Manager does, leaving its registration in place so it can be
    /// switched back (idempotent; a no-op if the entry no longer exists).
    /// </summary>
    void SetEnabled(StartupEntry entry, bool enabled);
}
