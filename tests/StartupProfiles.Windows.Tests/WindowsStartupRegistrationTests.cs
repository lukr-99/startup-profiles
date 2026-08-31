using Microsoft.Win32;
using StartupProfiles.Windows;

namespace StartupProfiles.Windows.Tests;

/// <summary>
/// Platform test for <see cref="WindowsStartupRegistration"/> against a throwaway HKCU subkey, so it
/// never touches the real Run key. Windows-only.
/// </summary>
public sealed class WindowsStartupRegistrationTests : IDisposable
{
    private readonly string _keyPath = $@"Software\StartupProfilesTests\{Guid.NewGuid():N}\Run";

    [Fact]
    public void EnableThenDisable_RoundTripsRunKeyValue()
    {
        if (!OperatingSystem.IsWindows()) return;

        var registration = new WindowsStartupRegistration("StartupProfilesTest", _keyPath);

        Assert.False(registration.IsEnabled());

        registration.Enable("\"C:\\Program Files\\StartupProfiles\\StartupProfiles.exe\"");
        Assert.True(registration.IsEnabled());

        registration.Disable();
        Assert.False(registration.IsEnabled());
    }

    [Fact]
    public void Enable_IsIdempotent()
    {
        if (!OperatingSystem.IsWindows()) return;

        var registration = new WindowsStartupRegistration("StartupProfilesTest", _keyPath);
        registration.Enable("cmd1");
        registration.Enable("cmd2");

        Assert.True(registration.IsEnabled());
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\StartupProfilesTests", throwOnMissingSubKey: false); }
            catch (System.Security.SecurityException) { }
        }
    }
}
