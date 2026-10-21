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

    /// <summary>The icon keys that have a Fluent glyph.</summary>
    public static IReadOnlyCollection<string> KnownKeys => Keys.Keys;

    public static (string Glyph, ProfileIconKind Kind) Resolve(string? icon, string name)
    {
        var trimmed = icon?.Trim();
        if (trimmed is { Length: > 0 } && Keys.TryGetValue(trimmed, out var glyph))
            return (glyph, ProfileIconKind.Fluent);

        var fallback = ProfileGlyph.For(icon, name);
        return (fallback, ProfileGlyph.IsSymbol(trimmed) ? ProfileIconKind.Emoji : ProfileIconKind.Initial);
    }
}
