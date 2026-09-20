using StartupProfiles.App.Ui;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Launcher;

/// <summary>A profile as shown on the launcher grid. <see cref="Glyph"/> is the profile's emoji icon, or
/// the first letter of its name when no icon is set (see <see cref="ProfileGlyph"/>).</summary>
public sealed record ProfileTile(string Id, string Name, string Glyph, bool IsEmoji)
{
    public static ProfileTile From(Profile profile) => new(
        profile.Id,
        profile.Name,
        ProfileGlyph.For(profile.Icon, profile.Name),
        ProfileGlyph.IsSymbol(profile.Icon?.Trim()));
}
