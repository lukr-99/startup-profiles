namespace StartupProfiles.Core.Integration;

/// <summary>
/// Where an approved <see cref="RegistrationRequest"/> should land: any number of profiles, the base, or
/// nowhere at all. Nowhere is not "do nothing": the app is still kept in the global library
/// (<see cref="IsLibraryOnly"/>), which is what the confirmation window's "Just recognize" offers, so the
/// user can add it to a profile whenever they want.
/// </summary>
public sealed record RegistrationTargets
{
    /// <summary>Recognize the app only - a library entry, no profile and no base action.</summary>
    public static RegistrationTargets LibraryOnly { get; } = new();

    /// <summary>Ids of the profiles that should gain a launch action.</summary>
    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    /// <summary>Add the launch action to the base, so it runs before every profile that includes the base.</summary>
    public bool IncludeBase { get; init; }

    /// <summary>True when nothing but the library entry was chosen.</summary>
    public bool IsLibraryOnly => ProfileIds.Count == 0 && !IncludeBase;

    /// <summary>Targets for the chosen profiles, optionally including the base.</summary>
    public static RegistrationTargets For(IEnumerable<string> profileIds, bool includeBase = false) =>
        new() { ProfileIds = [.. profileIds], IncludeBase = includeBase };
}
