using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Launcher;

/// <summary>A profile as shown on the launcher grid.</summary>
public sealed record ProfileTile(string Id, string Name, string Initial)
{
    public static ProfileTile From(Profile profile)
    {
        var name = string.IsNullOrWhiteSpace(profile.Name) ? "?" : profile.Name.Trim();
        return new ProfileTile(profile.Id, profile.Name, name[..1].ToUpperInvariant());
    }
}
