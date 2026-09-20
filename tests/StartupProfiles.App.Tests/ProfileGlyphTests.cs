using StartupProfiles.App.Ui;

namespace StartupProfiles.App.Tests;

public sealed class ProfileGlyphTests
{
    [Fact]
    public void For_UsesTheEmojiIcon() => Assert.Equal("🎮", ProfileGlyph.For("🎮", "Games"));

    [Fact]
    public void For_FallsBackToTheInitial_ForLegacyTextIcons() =>
        Assert.Equal("W", ProfileGlyph.For("work", "Work"));

    [Fact]
    public void For_FallsBackToTheInitial_WithoutAnIcon() =>
        Assert.Equal("S", ProfileGlyph.For(null, "school"));

    [Fact]
    public void For_WithoutANameOrIcon_IsAQuestionMark() =>
        Assert.Equal("?", ProfileGlyph.For("  ", "  "));
}
