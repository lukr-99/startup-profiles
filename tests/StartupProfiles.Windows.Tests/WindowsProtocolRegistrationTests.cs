using Microsoft.Win32;
using StartupProfiles.Windows;

namespace StartupProfiles.Windows.Tests;

/// <summary>
/// Platform test for <see cref="WindowsProtocolRegistration"/> against a throwaway HKCU classes subtree,
/// so it never touches the real per-user classes. Windows-only.
/// </summary>
public sealed class WindowsProtocolRegistrationTests : IDisposable
{
    private const string TestRoot = @"Software\StartupProfilesProtocolTests";
    private readonly string _classesPath = $@"{TestRoot}\{Guid.NewGuid():N}";

    [Fact]
    public void RegisterThenUnregister_RoundTripsProtocolKey()
    {
        if (!OperatingSystem.IsWindows()) return;

        var registration = new WindowsProtocolRegistration("startupprofiles", _classesPath);

        Assert.False(registration.IsRegistered());

        registration.Register(@"C:\Program Files\StartupProfiles\StartupProfiles.exe");
        Assert.True(registration.IsRegistered());

        using (var command = Registry.CurrentUser.OpenSubKey($@"{_classesPath}\startupprofiles\shell\open\command"))
        {
            Assert.Equal(
                "\"C:\\Program Files\\StartupProfiles\\StartupProfiles.exe\" \"%1\"",
                command!.GetValue(null));
        }

        registration.Unregister();
        Assert.False(registration.IsRegistered());
    }

    [Fact]
    public void Register_IsIdempotent()
    {
        if (!OperatingSystem.IsWindows()) return;

        var registration = new WindowsProtocolRegistration("startupprofiles", _classesPath);
        registration.Register("first.exe");
        registration.Register("second.exe");

        Assert.True(registration.IsRegistered());
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(TestRoot, throwOnMissingSubKey: false); }
            catch (System.Security.SecurityException) { }
        }
    }
}
