using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Startup;

/// <summary>One app Windows is set up to start at login, as reported by an <see cref="Windows.IStartupAppCatalog"/>.</summary>
public sealed record StartupEntry
{
    public required StartupEntrySource Source { get; init; }

    /// <summary>Unique within <see cref="Source"/>: the registry value name, the file name, or <c>PackageFamilyName\TaskId</c>.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>True if Windows will start it at the next login.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// True if the current user can switch it on or off without elevation. False for all-users entries and
    /// policy-locked packaged tasks.
    /// </summary>
    public bool CanToggle { get; init; }

    /// <summary>The action a profile uses to start it instead, or null when the command could not be resolved.</summary>
    public ProfileAction? Launch { get; init; }
}
