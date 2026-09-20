using System.Text;

namespace StartupProfiles.App.Ui;

/// <summary>
/// The single character that stands for a profile wherever one is listed: its emoji icon, or the first
/// letter of its name when no icon is set. Legacy text icons ("work", "dev") are not symbols, so they
/// fall back to the initial instead of being printed as words.
/// </summary>
public static class ProfileGlyph
{
    /// <summary>The glyph for a profile with this icon and name.</summary>
    public static string For(string? icon, string name)
    {
        var trimmed = icon?.Trim();
        if (IsSymbol(trimmed)) return trimmed!;

        var text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim();
        return text[..1].ToUpperInvariant();
    }

    /// <summary>True when the icon is a symbol rune (anything above the Latin/punctuation range).</summary>
    public static bool IsSymbol(string? icon) =>
        !string.IsNullOrEmpty(icon) && icon.EnumerateRunes().Any(r => r.Value >= 0x2190);
}
