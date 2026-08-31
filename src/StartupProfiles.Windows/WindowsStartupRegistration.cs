using Microsoft.Win32;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Windows;

/// <summary>
/// Registers the launcher under the current user's <c>Run</c> key
/// (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>) so it starts at login. Per-user, so it
/// needs no elevation. The key path is injectable for tests.
/// </summary>
public sealed class WindowsStartupRegistration : IStartupRegistration
{
    private const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _runKeyPath;
    private readonly string _valueName;

    public WindowsStartupRegistration(string valueName = "StartupProfiles", string? runKeyPath = null)
    {
        _valueName = valueName;
        _runKeyPath = runKeyPath ?? DefaultRunKeyPath;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
        return key?.GetValue(_valueName) is not null;
    }

    public void Enable(string commandLine)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath);
        key.SetValue(_valueName, commandLine, RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
        if (key?.GetValue(_valueName) is not null) key.DeleteValue(_valueName);
    }
}
