namespace StartupProfiles.Core.Models;

/// <summary>
/// The base: actions that run before the chosen profile's own, for every profile with
/// <see cref="Profile.IncludeBase"/> on. Not a profile itself - it has no launcher tile, name, or icon.
/// Persisted as base.json.
/// </summary>
public sealed record StartupBase
{
    public IReadOnlyList<ProfileAction> Actions { get; init; } = [];
}
