using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Launcher;

/// <summary>
/// A profile as shown on the launcher grid. <see cref="Glyph"/> comes from <see cref="ProfileIcon"/>: a Fluent icon for a known key, the
/// emoji itself, or the first letter of the name. <see cref="Number"/> is its 1-9 shortcut
/// key, or null past nine; <see cref="IsLast"/> marks the profile that ran last.
/// </summary>
public sealed record ProfileTile(string Id, string Name, string Glyph, bool IsEmoji)
{
    public int? Number { get; init; }
    public string CountText { get; init; } = "";
    public bool IsLast { get; init; }
    public StartupBehaviour StartupBehaviour { get; init; }

    /// <summary>True when <see cref="Glyph"/> is a Segoe Fluent Icons code point, not text.</summary>
    public bool IsFluent { get; init; }

    public static ProfileTile From(Profile profile, int index = 0, bool isLast = false)
    {
        var (glyph, kind) = ProfileIcon.Resolve(profile.Icon, profile.Id, profile.Name);
        return Build(profile, glyph, kind, index, isLast);
    }

    private static ProfileTile Build(Profile profile, string glyph, ProfileIconKind kind, int index, bool isLast) =>
        new(profile.Id, profile.Name, glyph, kind == ProfileIconKind.Emoji)
        {
            IsFluent = kind == ProfileIconKind.Fluent,
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
