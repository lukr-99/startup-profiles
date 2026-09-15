using System.IO;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Ui;

/// <summary>
/// Picks the shell item whose icon stands for an action in the editor's action table - the same icons Windows
/// shows on its Startup apps page: the app's exe, the shortcut, or the packaged app itself.
/// </summary>
public static class ActionIconSource
{
    /// <summary>Extension whose registered icon marks a URL (Internet Shortcut, i.e. the default browser).</summary>
    public const string UrlIcon = ".url";

    private const string AppsFolderPrefix = @"shell:AppsFolder\";

    /// <summary>
    /// A shell parsing name (file path or <c>shell:AppsFolder\...</c>), a ".ext" whose registered icon to use,
    /// or null when the action has no meaningful icon (delays, kills, services, inline commands).
    /// </summary>
    public static string? For(ActionType type, string? target, string? arguments)
    {
        var path = Unquote(target);
        var args = arguments?.Trim() ?? "";

        return type switch
        {
            // Packaged apps launch as "explorer.exe shell:AppsFolder\<family>!<app>"; show the app, not Explorer.
            ActionType.LaunchApp when IsExplorer(path) && args.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase) => args,
            // Squirrel apps (Discord, ...) launch as "Update.exe --processStart App.exe"; show App.exe, which
            // ShellIcons finds in the newest app-<version> folder.
            ActionType.LaunchApp when SquirrelApp(path, args) is { } app => app,
            ActionType.LaunchApp or ActionType.OpenFile or ActionType.OpenFolder when path.Length > 0 => path,
            ActionType.OpenUrl when path.Length > 0 => UrlIcon,
            ActionType.RunScript when Path.IsPathFullyQualified(path) => path,
            _ => null,
        };
    }

    private static bool IsExplorer(string path) =>
        string.Equals(Path.GetFileName(path), "explorer.exe", StringComparison.OrdinalIgnoreCase);

    private static string? SquirrelApp(string path, string args)
    {
        const string flag = "--processStart";
        if (!string.Equals(Path.GetFileName(path), "Update.exe", StringComparison.OrdinalIgnoreCase)) return null;

        var index = args.IndexOf(flag, StringComparison.OrdinalIgnoreCase);
        if (index < 0 || Path.GetDirectoryName(path) is not { Length: > 0 } directory) return null;

        var rest = args[(index + flag.Length)..].TrimStart();
        var app = rest.StartsWith('"')
            ? rest[1..].Split('"')[0]
            : rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(app) ? null : Path.Combine(directory, app);
    }

    private static string Unquote(string? value) => value?.Trim().Trim('"').Trim() ?? "";
}
