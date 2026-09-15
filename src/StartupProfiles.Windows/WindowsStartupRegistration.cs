using Microsoft.Win32;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Windows;

/// <summary>
/// Registers the launcher under the current user's <c>Run</c> key
/// (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>) so it starts at login. Per-user, so it
/// needs no elevation. The key paths are injectable for tests.
/// </summary>
public sealed class WindowsStartupRegistration : IStartupRegistration
{
    /// <summary>The launcher's <c>Run</c> value name.</summary>
    public const string DefaultValueName = "StartupProfiles";

    private const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DefaultApprovedKeyPath = StartupApprovedFlag.RootPath + @"\Run";

    private readonly string _runKeyPath;
    private readonly string _approvedKeyPath;
    private readonly string _valueName;

    public WindowsStartupRegistration(
        string valueName = DefaultValueName, string? runKeyPath = null, string? approvedKeyPath = null)
    {
        _valueName = valueName;
        _runKeyPath = runKeyPath ?? DefaultRunKeyPath;
        _approvedKeyPath = approvedKeyPath ?? DefaultApprovedKeyPath;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
        if (key?.GetValue(_valueName) is null) return false;

        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKeyPath);
        return StartupApprovedFlag.IsEnabled(approved, _valueName);
    }

    public void Enable(string commandLine)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_runKeyPath))
        {
            key.SetValue(_valueName, commandLine, RegistryValueKind.String);
        }

        // A value switched off in Task Manager stays off even when rewritten, so clear that flag too.
        ClearApprovedFlag();
    }

    public void Disable()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true))
        {
            if (key?.GetValue(_valueName) is not null) key.DeleteValue(_valueName);
        }

        ClearApprovedFlag();
    }

    private void ClearApprovedFlag()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(_approvedKeyPath, writable: true);
        if (approved?.GetValue(_valueName) is not null) approved.DeleteValue(_valueName);
    }
}
