using StartupProfiles.App.Ui;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Launcher;

/// <summary>
/// A profile as shown on the launcher grid. <see cref="Glyph"/> is the profile's emoji icon, or the first letter
/// of its name when no icon is set (see <see cref="ProfileGlyph"/>). <see cref="Number"/> is its 1-9 shortcut
/// key, or null past nine; <see cref="IsLast"/> marks the profile that ran last.
/// </summary>
public sealed record ProfileTile(string Id, string Name, string Glyph, bool IsEmoji)
{
    public int? Number { get; init; }
    public string CountText { get; init; } = "";
    public bool IsLast { get; init; }
    public StartupBehaviour StartupBehaviour { get; init; }

    public static ProfileTile From(Profile profile, int index = 0, bool isLast = false) => new(
        profile.Id,
        profile.Name,
        ProfileGlyph.For(profile.Icon, profile.Name),
        ProfileGlyph.IsSymbol(profile.Icon?.Trim()))
    {
        Number = index < 9 ? index + 1 : null,
        CountText = profile.Actions.Count switch
        {
            0 => "Nothing yet",
            1 => "1 step",
            var n => $"{n} steps",
        },
        IsLast = isLast,
        StartupBehaviour = profile.StartupBehaviour,
    };
}
