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
    private readonly string _approvedPath = $@"Software\StartupProfilesTests\{Guid.NewGuid():N}\StartupApproved\Run";

    [Fact]
    public void EnableThenDisable_RoundTripsRunKeyValue()
    {
        if (!OperatingSystem.IsWindows()) return;

        var registration = new WindowsStartupRegistration("StartupProfilesTest", _keyPath, _approvedPath);

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

        var registration = new WindowsStartupRegistration("StartupProfilesTest", _keyPath, _approvedPath);
        registration.Enable("cmd1");
        registration.Enable("cmd2");

        Assert.True(registration.IsEnabled());
    }

    [Fact]
    public void Enable_ClearsAFlagThatSwitchedTheLauncherOff()
    {
        if (!OperatingSystem.IsWindows()) return;

        var registration = new WindowsStartupRegistration("StartupProfilesTest", _keyPath, _approvedPath);
        registration.Enable("cmd");
        using (var approved = Registry.CurrentUser.CreateSubKey(_approvedPath))
        {
            approved.SetValue("StartupProfilesTest", new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
        }

        Assert.False(registration.IsEnabled());

        registration.Enable("cmd");

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
