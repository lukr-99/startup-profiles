using Microsoft.Win32;

namespace StartupProfiles.Windows;

/// <summary>
/// The on/off switch Task Manager and Settings keep for <c>Run</c> key and Startup folder entries under
/// <c>...\Explorer\StartupApproved\{Run,Run32,StartupFolder}</c>. Each value is a 12-byte REG_BINARY named
/// after the entry: an even first byte (02, 06) means enabled, an odd one (01, 03) disabled, and bytes 4-11
/// hold the FILETIME it was disabled. No value means enabled.
/// </summary>
internal static class StartupApprovedFlag
{
    public const string RootPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    public static bool IsEnabled(RegistryKey? approvedKey, string name) =>
        approvedKey?.GetValue(name) is not byte[] { Length: > 0 } flag || (flag[0] & 1) == 0;

    public static void Set(RegistryKey approvedKey, string name, bool enabled, DateTimeOffset now)
    {
        var flag = new byte[12];
        flag[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled) BitConverter.TryWriteBytes(flag.AsSpan(4), now.ToFileTime());
        approvedKey.SetValue(name, flag, RegistryValueKind.Binary);
    }
}
