namespace StartupProfiles.Core.Integration;

/// <summary>
/// Result of applying a <see cref="RegistrationRequest"/> to a set of chosen profiles: which profiles
/// gained a launch action, which already had one for this target (skipped), and which ids were unknown.
/// </summary>
public sealed record RegistrationOutcome(
    IReadOnlyList<string> AddedTo,
    IReadOnlyList<string> AlreadyPresentIn,
    IReadOnlyList<string> UnknownProfileIds)
{
    /// <summary>True when the request changed at least one profile.</summary>
    public bool ChangedAnything => AddedTo.Count > 0;
}
