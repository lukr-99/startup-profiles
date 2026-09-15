namespace StartupProfiles.Core.Startup;

/// <summary>Where Windows keeps a <see cref="StartupEntry"/> - the same sources Task Manager's Startup apps page lists.</summary>
public enum StartupEntrySource
{
    /// <summary>Current user's <c>Run</c> registry key.</summary>
    UserRunKey,

    /// <summary>All-users <c>Run</c> registry key (64-bit view).</summary>
    MachineRunKey,

    /// <summary>All-users <c>Run</c> registry key (32-bit / WOW6432Node view).</summary>
    MachineRunKey32,

    /// <summary>Current user's Startup folder.</summary>
    UserStartupFolder,

    /// <summary>All-users Startup folder.</summary>
    CommonStartupFolder,

    /// <summary>A packaged (MSIX / Store) app's declared startup task.</summary>
    PackagedTask,
}
