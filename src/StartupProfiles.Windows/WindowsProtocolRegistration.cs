using Microsoft.Win32;
using StartupProfiles.Core.Integration;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Windows;

/// <summary>
/// Registers the <c>startupprofiles://</c> scheme under the current user's classes
/// (<c>HKCU\Software\Classes\startupprofiles</c>) so registration links launch the app with the URI as
/// <c>"%1"</c>. Per-user, so it needs no elevation. The classes root and scheme are injectable for tests.
/// </summary>
public sealed class WindowsProtocolRegistration : IProtocolRegistration
{
    private const string DefaultClassesPath = @"Software\Classes";

    private readonly string _schemePath;

    public WindowsProtocolRegistration(string? scheme = null, string? classesPath = null)
    {
        var effectiveScheme = scheme ?? RegistrationRequestParser.Scheme;
        _schemePath = $@"{classesPath ?? DefaultClassesPath}\{effectiveScheme}";
    }

    public bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{_schemePath}\shell\open\command");
        return key?.GetValue(null) is string command && !string.IsNullOrWhiteSpace(command);
    }

    public void Register(string executablePath)
    {
        using var scheme = Registry.CurrentUser.CreateSubKey(_schemePath);
        scheme.SetValue(null, "URL:Startup Profiles Protocol", RegistryValueKind.String);
        scheme.SetValue("URL Protocol", "", RegistryValueKind.String);

        using var command = Registry.CurrentUser.CreateSubKey($@"{_schemePath}\shell\open\command");
        command.SetValue(null, $"\"{executablePath}\" \"%1\"", RegistryValueKind.String);
    }

    public void Unregister()
    {
        using var parent = Registry.CurrentUser.OpenSubKey(ParentPath(_schemePath), writable: true);
        parent?.DeleteSubKeyTree(LeafName(_schemePath), throwOnMissingSubKey: false);
    }

    private static string ParentPath(string path) => path[..path.LastIndexOf('\\')];

    private static string LeafName(string path) => path[(path.LastIndexOf('\\') + 1)..];
}
