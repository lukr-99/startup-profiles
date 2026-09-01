using System.Text;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Launcher;

/// <summary>A profile as shown on the launcher grid. <see cref="Glyph"/> is the profile's emoji icon, or
/// the first letter of its name when no icon is set.</summary>
public sealed record ProfileTile(string Id, string Name, string Glyph, bool IsEmoji)
{
    public static ProfileTile From(Profile profile)
    {
        var name = string.IsNullOrWhiteSpace(profile.Name) ? "?" : profile.Name.Trim();
        var icon = profile.Icon?.Trim();
        return HasSymbol(icon)
            ? new ProfileTile(profile.Id, profile.Name, icon!, IsEmoji: true)
            : new ProfileTile(profile.Id, profile.Name, name[..1].ToUpperInvariant(), IsEmoji: false);
    }

    // Treat the icon as an emoji when it contains a symbol rune (anything above the Latin/punctuation
    // range); plain text like the legacy default "dev"/"work" falls back to the letter initial.
    private static bool HasSymbol(string? value) =>
        !string.IsNullOrEmpty(value) && value.EnumerateRunes().Any(r => r.Value >= 0x2190);
}
