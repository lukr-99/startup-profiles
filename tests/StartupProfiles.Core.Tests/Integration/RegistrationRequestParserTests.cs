using StartupProfiles.Core.Integration;

namespace StartupProfiles.Core.Tests.Integration;

public sealed class RegistrationRequestParserTests
{
    [Fact]
    public void TryParseUri_FullQuery_MapsEveryField()
    {
        const string uri = "startupprofiles://register?appId=com.example.app&name=Example%20App" +
            "&target=C%3A%5CApps%5Cexample.exe&args=--fast&icon=example.ico&publisher=Example%20Inc" +
            "&suggestedProfile=dev&supportsMinimized=true";

        Assert.True(RegistrationRequestParser.TryParseUri(uri, out var request, out var error));
        Assert.Equal("", error);
        Assert.Equal("com.example.app", request.AppId);
        Assert.Equal("Example App", request.Name);
        Assert.Equal(@"C:\Apps\example.exe", request.Target);
        Assert.Equal("--fast", request.Arguments);
        Assert.Equal("example.ico", request.Icon);
        Assert.Equal("Example Inc", request.Publisher);
        Assert.Equal("dev", request.SuggestedProfile);
        Assert.True(request.SupportsMinimized);
    }

    [Fact]
    public void TryParseUri_OpaqueForm_IsAccepted()
    {
        const string uri = "startupprofiles:register?appId=a&name=A&target=a.exe";

        Assert.True(RegistrationRequestParser.TryParseUri(uri, out var request, out _));
        Assert.Equal("a", request.AppId);
    }

    [Fact]
    public void TryParseUri_WrongScheme_Fails()
    {
        Assert.False(RegistrationRequestParser.TryParseUri(
            "https://register?appId=a&name=A&target=a.exe", out _, out var error));
        Assert.Contains("registration URI", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseUri_UnsupportedAction_Fails()
    {
        Assert.False(RegistrationRequestParser.TryParseUri(
            "startupprofiles://remove?appId=a", out _, out var error));
        Assert.Contains("Unsupported action", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParseUri_MissingTarget_ReportsMissingField()
    {
        Assert.False(RegistrationRequestParser.TryParseUri(
            "startupprofiles://register?appId=a&name=A", out _, out var error));
        Assert.Contains("target", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseArguments_KebabFlags_MapToFields()
    {
        string[] args =
        [
            "register",
            "--app-id", "com.example.app",
            "--name", "Example App",
            "--target", @"C:\Apps\example.exe",
            "--args", "--fast",
            "--suggested-profile", "games",
            "--supports-minimized",
        ];

        Assert.True(RegistrationRequestParser.TryParseArguments(args, out var request, out _));
        Assert.Equal("com.example.app", request.AppId);
        Assert.Equal("Example App", request.Name);
        Assert.Equal(@"C:\Apps\example.exe", request.Target);
        Assert.Equal("--fast", request.Arguments);
        Assert.Equal("games", request.SuggestedProfile);
        Assert.True(request.SupportsMinimized);
    }

    [Fact]
    public void TryParseArguments_NotRegisterCommand_Fails()
    {
        Assert.False(RegistrationRequestParser.TryParseArguments(["--headless"], out _, out _));
    }

    [Fact]
    public void TryParseArguments_OptionMissingValue_Fails()
    {
        string[] args = ["register", "--app-id", "--name", "A"];

        Assert.False(RegistrationRequestParser.TryParseArguments(args, out _, out var error));
        Assert.Contains("expects a value", error, StringComparison.Ordinal);
    }
}
