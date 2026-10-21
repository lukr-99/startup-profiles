using StartupProfiles.App.Ui;

namespace StartupProfiles.App.Launcher;

/// <summary>
/// Picks the launcher tile icon for a profile's <c>icon</c> value. Known keys map to Segoe Fluent Icons so
/// every tile shares one style; an emoji is kept as it is, and anything else falls back to the name's initial.
/// </summary>
public static class ProfileIcon
{
    private static readonly Dictionary<string, string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["work"] = "",       // Work
        ["school"] = "",     // Education
        ["games"] = "",      // Game
        ["chill"] = "",      // Headphone
        ["everything"] = "", // All apps
        ["code"] = "",       // Code
        ["dev"] = "",
        ["deadlock"] = "",   // Lightning bolt
    };

    // The emoji the starter profiles ship with, so existing data gets a Fluent icon without being edited.
    private static readonly Dictionary<string, string> DefaultEmoji = new()
    {
        ["💼"] = "work",
        ["💻"] = "code",
        ["🎓"] = "school",
        ["🎮"] = "games",
        ["🎧"] = "chill",
        ["✨"] = "everything",
    };

    /// <summary>The icon keys that have a Fluent glyph.</summary>
    public static IReadOnlyCollection<string> KnownKeys => Keys.Keys;

    /// <summary>
    /// Tries the profile id as a key first (so a profile named "deadlock" gets its own icon even when its
    /// <c>icon</c> says "games"), then the icon as a key, then a starter emoji, then the emoji or initial.
    /// </summary>
    public static (string Glyph, ProfileIconKind Kind) Resolve(string? icon, string id, string name)
    {
        var trimmed = icon?.Trim();
        var glyph = Lookup(id?.Trim()) ?? Lookup(trimmed)
            ?? (trimmed is not null && DefaultEmoji.TryGetValue(trimmed, out var named) ? Lookup(named) : null);
        if (glyph is not null) return (glyph, ProfileIconKind.Fluent);

        var fallback = ProfileGlyph.For(icon, name);
        return (fallback, ProfileGlyph.IsSymbol(trimmed) ? ProfileIconKind.Emoji : ProfileIconKind.Initial);
    }

    private static string? Lookup(string? key) =>
        !string.IsNullOrEmpty(key) && Keys.TryGetValue(key, out var glyph) ? glyph : null;
}
