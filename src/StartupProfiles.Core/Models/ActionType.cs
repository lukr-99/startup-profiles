namespace StartupProfiles.Core.Models;

/// <summary>The kind of work a <see cref="ProfileAction"/> performs when a profile runs.</summary>
public enum ActionType
{
    LaunchApp,
    OpenUrl,
    OpenFile,
    OpenFolder,
    RunScript,
    KillProcess,
    Delay,

    // Deferred to a later milestone: these need Windows service/VPN APIs and have no handler yet.
    StartService,
    StartVpn,
}
