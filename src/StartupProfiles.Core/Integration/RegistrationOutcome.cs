namespace StartupProfiles.Core.Integration;

/// <summary>
/// Result of applying a <see cref="RegistrationRequest"/> to the chosen <see cref="RegistrationTargets"/>:
/// the library entry the app is now known as, whether the base gained a launch action, which profiles
/// gained one, which already started the app (skipped), and which ids were unknown.
/// </summary>
public sealed record RegistrationOutcome(
    IReadOnlyList<string> AddedTo,
    IReadOnlyList<string> AlreadyPresentIn,
    IReadOnlyList<string> UnknownProfileIds,
    string? LibraryItemId = null,
    bool AddedToLibrary = false,
    bool AddedToBase = false,
    bool AlreadyInBase = false)
{
    /// <summary>True when the request wrote anything - a profile, the base, or a new library entry.</summary>
    public bool ChangedAnything => AddedTo.Count > 0 || AddedToBase || AddedToLibrary;
}
