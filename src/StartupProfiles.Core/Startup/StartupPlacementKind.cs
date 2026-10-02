namespace StartupProfiles.Core.Startup;

/// <summary>The kind of <see cref="StartupPlacement"/>.</summary>
public enum StartupPlacementKind
{
    /// <summary>Change nothing: Windows keeps starting it.</summary>
    LeaveInWindows,

    /// <summary>Keep it in the library to drag into a profile later; Windows keeps starting it.</summary>
    LibraryOnly,

    /// <summary>Start it from the base, and switch it off in Windows.</summary>
    Base,

    /// <summary>Start it from one profile, and switch it off in Windows.</summary>
    Profile,
}
