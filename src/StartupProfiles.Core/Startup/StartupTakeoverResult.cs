namespace StartupProfiles.Core.Startup;

/// <summary>What <see cref="StartupTakeover.TakeOver"/> changed.</summary>
/// <param name="Adopted">Added to the Everything profile and switched off in Windows.</param>
/// <param name="LeftEnabled">Still on in Windows because the user cannot switch it off (or it cannot be launched from a profile).</param>
/// <param name="Failed">Added to the Everything profile, but switching it off in Windows failed.</param>
public sealed record StartupTakeoverResult(
    IReadOnlyList<StartupEntry> Adopted,
    IReadOnlyList<StartupEntry> LeftEnabled,
    IReadOnlyList<StartupEntry> Failed);
