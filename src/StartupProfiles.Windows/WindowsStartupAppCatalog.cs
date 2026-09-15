using System.Diagnostics;
using System.Security;
using Microsoft.Win32;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Windows;

/// <summary>
/// Reads the same startup sources as Task Manager's Startup apps page: the per-user and all-users <c>Run</c>
/// keys, the per-user and all-users Startup folders, and packaged apps' startup tasks. On/off state comes from
/// the <c>StartupApproved</c> flags (see <see cref="StartupApprovedFlag"/>) and each task's <c>State</c> value.
/// Only per-user entries are switchable, so nothing here needs elevation. Registry roots and folders are
/// injectable so tests run against a throwaway HKCU subkey and temp folders.
/// </summary>
public sealed class WindowsStartupAppCatalog : IStartupAppCatalog
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32Path = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string AppModelPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel";
    private const string SystemAppDataPath = AppModelPath + @"\SystemAppData";
    private const string PackagesPath = AppModelPath + @"\Repository\Packages";

    // Packaged startup task State values (Windows.ApplicationModel.StartupTaskState).
    private const int TaskDisabled = 0;
    private const int TaskDisabledByUser = 1;
    private const int TaskEnabled = 2;
    private const int TaskEnabledByPolicy = 4;

    private readonly RegistryKey _userRoot;
    private readonly RegistryKey _machineRoot;
    private readonly string _userStartupFolder;
    private readonly string _commonStartupFolder;
    private readonly TimeProvider _time;

    public WindowsStartupAppCatalog()
        : this(
            Registry.CurrentUser,
            Registry.LocalMachine,
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            TimeProvider.System)
    {
    }

    public WindowsStartupAppCatalog(
        RegistryKey userRoot,
        RegistryKey machineRoot,
        string userStartupFolder,
        string commonStartupFolder,
        TimeProvider time)
    {
        _userRoot = userRoot;
        _machineRoot = machineRoot;
        _userStartupFolder = userStartupFolder;
        _commonStartupFolder = commonStartupFolder;
        _time = time;
    }

    public IReadOnlyList<StartupEntry> GetEntries() =>
    [
        .. RunKeyEntries(_userRoot, RunPath, "Run", StartupEntrySource.UserRunKey, canToggle: true),
        .. RunKeyEntries(_machineRoot, RunPath, "Run", StartupEntrySource.MachineRunKey, canToggle: false),
        .. RunKeyEntries(_machineRoot, Run32Path, "Run32", StartupEntrySource.MachineRunKey32, canToggle: false),
        .. FolderEntries(_userRoot, _userStartupFolder, StartupEntrySource.UserStartupFolder, canToggle: true),
        .. FolderEntries(_machineRoot, _commonStartupFolder, StartupEntrySource.CommonStartupFolder, canToggle: false),
        .. PackagedTaskEntries(),
    ];

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        switch (entry.Source)
        {
            case StartupEntrySource.UserRunKey:
                if (RunValueExists(entry.Key)) SetApproved("Run", entry.Key, enabled);
                break;
            case StartupEntrySource.UserStartupFolder:
                if (StartupFileExists(entry.Key)) SetApproved("StartupFolder", entry.Key, enabled);
                break;
            case StartupEntrySource.PackagedTask:
                SetTaskState(entry.Key, enabled);
                break;
            default:
                throw new InvalidOperationException($"'{entry.Name}' starts for all users; switching it needs elevation.");
        }
    }

    private static List<StartupEntry> RunKeyEntries(
        RegistryKey root, string runPath, string approvedName, StartupEntrySource source, bool canToggle)
    {
        using var run = TryOpen(root, runPath);
        if (run is null) return [];
        using var approved = TryOpen(root, $@"{StartupApprovedFlag.RootPath}\{approvedName}");

        var entries = new List<StartupEntry>();
        foreach (var name in run.GetValueNames())
        {
            if (name.Length == 0 || run.GetValue(name) is not string command) continue;
            var launch = StartupCommandLine.ToLaunchAction(command);
            entries.Add(new StartupEntry
            {
                Source = source,
                Key = name,
                Name = DescribedName(launch, name),
                IsEnabled = StartupApprovedFlag.IsEnabled(approved, name),
                CanToggle = canToggle,
                Launch = launch,
            });
        }

        return entries;
    }

    /// <summary>
    /// The exe's file description, which is what Task Manager shows ("Microsoft Edge" rather than
    /// "MicrosoftEdgeAutoLaunch_DC0A..."), falling back to the registry value name. Squirrel's shared
    /// Update.exe describes itself as "Update", so it keeps the value name.
    /// </summary>
    private static string DescribedName(ProfileAction? launch, string valueName)
    {
        if (launch is null || !File.Exists(launch.Target) ||
            string.Equals(Path.GetFileName(launch.Target), "Update.exe", StringComparison.OrdinalIgnoreCase))
            return valueName;

        try
        {
            var description = FileVersionInfo.GetVersionInfo(launch.Target).FileDescription;
            return string.IsNullOrWhiteSpace(description) ? valueName : description.Trim();
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
        {
            return valueName;
        }
    }

    private static List<StartupEntry> FolderEntries(
        RegistryKey root, string folder, StartupEntrySource source, bool canToggle)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return [];
        using var approved = TryOpen(root, $@"{StartupApprovedFlag.RootPath}\StartupFolder");

        return [.. Directory.EnumerateFiles(folder)
            .Where(path => !string.Equals(Path.GetFileName(path), "desktop.ini", StringComparison.OrdinalIgnoreCase))
            .Select(path => new StartupEntry
            {
                Source = source,
                Key = Path.GetFileName(path),
                Name = Path.GetFileNameWithoutExtension(path),
                IsEnabled = StartupApprovedFlag.IsEnabled(approved, Path.GetFileName(path)),
                CanToggle = canToggle,
                // Shell-opening the shortcut keeps its arguments and working directory.
                Launch = new ProfileAction { Type = ActionType.OpenFile, Target = path },
            })];
    }

    private List<StartupEntry> PackagedTaskEntries()
    {
        using var systemAppData = TryOpen(_userRoot, SystemAppDataPath);
        if (systemAppData is null) return [];

        var entries = new List<StartupEntry>();
        foreach (var family in systemAppData.GetSubKeyNames())
        {
            using var familyKey = TryOpen(systemAppData, family);
            if (familyKey is null) continue;

            foreach (var taskId in familyKey.GetSubKeyNames())
            {
                using var taskKey = TryOpen(familyKey, taskId);
                if (taskKey?.GetValue("State") is not int state) continue;

                var app = FindPackagedApp(family, taskId);
                entries.Add(new StartupEntry
                {
                    Source = StartupEntrySource.PackagedTask,
                    Key = $@"{family}\{taskId}",
                    Name = app?.DisplayName ?? FallbackPackageName(family),
                    IsEnabled = state is TaskEnabled or TaskEnabledByPolicy,
                    CanToggle = state is TaskDisabled or TaskDisabledByUser or TaskEnabled,
                    Launch = app is null ? null : new ProfileAction
                    {
                        Type = ActionType.LaunchApp,
                        Target = "explorer.exe",
                        Arguments = $@"shell:AppsFolder\{family}!{app.AppId}",
                    },
                });
            }
        }

        return entries;
    }

    /// <summary>Resolves a package family's installed manifest and the application that owns <paramref name="taskId"/>.</summary>
    private PackagedStartupApp? FindPackagedApp(string family, string taskId)
    {
        // A family name is "Name_PublisherId"; an installed full name is "Name_Version_Arch_ResourceId_PublisherId".
        var separator = family.LastIndexOf('_');
        if (separator <= 0) return null;
        var prefix = family[..(separator + 1)];
        var suffix = family[separator..];

        using var packages = TryOpen(_userRoot, PackagesPath);
        if (packages is null) return null;

        var candidates = packages.GetSubKeyNames()
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderDescending(StringComparer.OrdinalIgnoreCase);

        foreach (var fullName in candidates)
        {
            using var package = TryOpen(packages, fullName);
            if (package?.GetValue("PackageRootFolder") is not string rootFolder) continue;

            var app = AppxManifestReader.FindStartupTask(Path.Combine(rootFolder, "AppxManifest.xml"), taskId);
            if (app is null) continue;

            return app.DisplayName is null && package.GetValue("DisplayName") is string packageName &&
                   !packageName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)
                ? app with { DisplayName = packageName }
                : app;
        }

        return null;
    }

    /// <summary>"Microsoft.YourPhone_8wekyb3d8bbwe" -> "YourPhone".</summary>
    private static string FallbackPackageName(string family)
    {
        var name = family.LastIndexOf('_') is > 0 and var separator ? family[..separator] : family;
        return name[(name.LastIndexOf('.') + 1)..];
    }

    private bool RunValueExists(string name)
    {
        using var run = TryOpen(_userRoot, RunPath);
        return run?.GetValue(name) is not null;
    }

    private bool StartupFileExists(string fileName) =>
        Path.GetFileName(fileName) == fileName && File.Exists(Path.Combine(_userStartupFolder, fileName));

    private void SetApproved(string approvedName, string name, bool enabled)
    {
        using var approved = _userRoot.CreateSubKey($@"{StartupApprovedFlag.RootPath}\{approvedName}");
        StartupApprovedFlag.Set(approved, name, enabled, _time.GetUtcNow());
    }

    private void SetTaskState(string key, bool enabled)
    {
        using var task = _userRoot.OpenSubKey($@"{SystemAppDataPath}\{key}", writable: true);
        if (task?.GetValue("State") is not int state) return;
        if (state is not (TaskDisabled or TaskDisabledByUser or TaskEnabled))
            throw new InvalidOperationException($"Startup task '{key}' is set by policy and cannot be switched.");

        task.SetValue("State", enabled ? TaskEnabled : TaskDisabledByUser, RegistryValueKind.DWord);
    }

    private static RegistryKey? TryOpen(RegistryKey parent, string path)
    {
        try
        {
            return parent.OpenSubKey(path);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
