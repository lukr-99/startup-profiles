using StartupProfiles.App.Launcher;

namespace StartupProfiles.App.Tests;

public sealed class ProfileIconTests
{
    [Theory]
    [InlineData("work")]
    [InlineData("school")]
    [InlineData("games")]
    [InlineData("chill")]
    [InlineData("everything")]
    [InlineData("code")]
    [InlineData("deadlock")]
    public void A_known_key_maps_to_a_fluent_glyph(string key)
    {
        var (glyph, kind) = ProfileIcon.Resolve(key, "Anything");
        Assert.Equal(ProfileIconKind.Fluent, kind);
        Assert.Single(glyph);
        Assert.InRange(glyph[0], '', '');
    }

    [Fact]
    public void Keys_ignore_case_and_padding() =>
        Assert.Equal(ProfileIcon.Resolve("code", "x"), ProfileIcon.Resolve("  CODE ", "x"));

    [Fact]
    public void Every_key_has_its_own_glyph_except_the_dev_alias()
    {
        var glyphs = ProfileIcon.KnownKeys.Where(k => k != "dev").Select(k => ProfileIcon.Resolve(k, "x").Glyph);
        Assert.Equal(glyphs.Count(), glyphs.Distinct().Count());
    }

    [Fact]
    public void An_emoji_is_kept_as_an_emoji()
    {
        var (glyph, kind) = ProfileIcon.Resolve("✨", "Everything");
        Assert.Equal("✨", glyph);
        Assert.Equal(ProfileIconKind.Emoji, kind);
    }

    [Theory]
    [InlineData("unknown-key", "Games", "G")]
    [InlineData(null, "school", "S")]
    [InlineData("  ", "  ", "?")]
    public void Anything_else_falls_back_to_the_initial(string? icon, string name, string expected)
    {
        var (glyph, kind) = ProfileIcon.Resolve(icon, name);
        Assert.Equal(expected, glyph);
        Assert.Equal(ProfileIconKind.Initial, kind);
    }
}
