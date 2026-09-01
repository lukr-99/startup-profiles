namespace StartupProfiles.Core.Windows;

/// <summary>
/// Registers (or unregisters) the <c>startupprofiles://</c> URL protocol so the OS routes registration
/// links to the app (docs/INTEGRATION.md). The seam is portable; the Windows adapter lives in
/// StartupProfiles.Windows so Core stays platform-independent and testable.
/// </summary>
public interface IProtocolRegistration
{
    /// <summary>True if the protocol currently points at an executable.</summary>
    bool IsRegistered();

    /// <summary>Registers the protocol to launch <paramref name="executablePath"/> with the URI (idempotent).</summary>
    void Register(string executablePath);

    /// <summary>Removes the protocol registration if present (idempotent).</summary>
    void Unregister();
}
