using StartupProfiles.App.Integration;

namespace StartupProfiles.App.Tests;

public sealed class RegistrationChoiceTests
{
    [Fact]
    public void ForProfile_ShowsWhatTheProfileAlreadyStarts()
    {
        Assert.Equal("nothing yet", RegistrationChoice.ForProfile("dev", "Dev", "💻", 0).Detail);
        Assert.Equal("1 item", RegistrationChoice.ForProfile("dev", "Dev", "💻", 1).Detail);
        Assert.Equal("4 items", RegistrationChoice.ForProfile("dev", "Dev", "💻", 4).Detail);
    }

    [Fact]
    public void ForProfile_TakesItsGlyphFromTheIcon()
    {
        Assert.Equal("💻", RegistrationChoice.ForProfile("dev", "Dev", "💻", 0).Glyph);
        Assert.Equal("D", RegistrationChoice.ForProfile("dev", "Dev", "dev", 0).Glyph);
    }

    [Fact]
    public void ForBase_SaysItStartsWithEveryProfile()
    {
        var choice = RegistrationChoice.ForBase(2);

        Assert.True(choice.IsBase);
        Assert.Equal("Base", choice.Name);
        Assert.Equal("Starts with every profile - 2 items", choice.Detail);
        Assert.False(choice.IsSelected);
    }
}
