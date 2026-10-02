namespace StartupProfiles.Core.Startup;

/// <summary>
/// Where a newly found startup app should start from: a profile or the base (then Windows stops starting it),
/// the library only, or nowhere new (Windows keeps starting it).
/// </summary>
/// <param name="ProfileId">The profile for <see cref="StartupPlacementKind.Profile"/>; null otherwise.</param>
public sealed record StartupPlacement(StartupPlacementKind Kind, string? ProfileId = null)
{
    public static StartupPlacement LeaveInWindows { get; } = new(StartupPlacementKind.LeaveInWindows);
    public static StartupPlacement LibraryOnly { get; } = new(StartupPlacementKind.LibraryOnly);
    public static StartupPlacement Base { get; } = new(StartupPlacementKind.Base);

    public static StartupPlacement Profile(string id) => new(StartupPlacementKind.Profile, id);
}
