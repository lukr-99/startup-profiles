using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Api;

/// <summary>Lightweight profile projection for the list endpoint (no action detail).</summary>
public sealed record ProfileSummary(string Id, string Name, string? Icon, int ActionCount)
{
    public static ProfileSummary From(Profile profile) =>
        new(profile.Id, profile.Name, profile.Icon, profile.Actions.Count);
}
