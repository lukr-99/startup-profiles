namespace StartupProfiles.App.Maintenance;

/// <summary>A one-shot install/uninstall command the app runs for the installer, then exits.</summary>
public enum MaintenanceCommand
{
    None,
    RegisterLogin,
    UnregisterLogin,
    RegisterProtocol,
    UnregisterProtocol,
    ListStartup,
    TakeOverStartup,
    RestoreStartup,
}
