using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>
/// Plain words for action types and failure behaviours, so the editor reads "App" and "Keep going" instead of
/// "LaunchApp" and "Continue". The stored values do not change.
/// </summary>
public static class ActionLabels
{
    public static IReadOnlyList<Choice<ActionType>> Types { get; } =
    [
        new(ActionType.LaunchApp, "App"),
        new(ActionType.OpenUrl, "Website"),
        new(ActionType.OpenFile, "File"),
        new(ActionType.OpenFolder, "Folder"),
        new(ActionType.RunScript, "Command"),
        new(ActionType.KillProcess, "Close an app"),
        new(ActionType.Delay, "Pause"),
        new(ActionType.StartService, "Windows service"),
        new(ActionType.StartVpn, "VPN"),
    ];

    public static IReadOnlyList<Choice<FailureBehaviour>> FailureBehaviours { get; } =
    [
        new(FailureBehaviour.Continue, "Keep going"),
        new(FailureBehaviour.Retry, "Try again"),
        new(FailureBehaviour.Stop, "Stop the profile"),
    ];

    public static string For(ActionType type) => Types.FirstOrDefault(c => c.Value == type)?.Label ?? type.ToString();

    /// <summary>What the target field holds for <paramref name="type"/>.</summary>
    public static string TargetLabel(ActionType type) => type switch
    {
        ActionType.LaunchApp => "Program",
        ActionType.OpenUrl => "Web address",
        ActionType.OpenFile => "File",
        ActionType.OpenFolder => "Folder",
        ActionType.RunScript => "Command",
        ActionType.KillProcess => "App to close (process name)",
        ActionType.Delay => "Note (optional)",
        ActionType.StartService => "Service name",
        ActionType.StartVpn => "VPN connection name",
        _ => "Target",
    };

    /// <summary>True when the target is a path the user can pick with a Browse dialog.</summary>
    public static bool CanBrowse(ActionType type) =>
        type is ActionType.LaunchApp or ActionType.OpenFile or ActionType.OpenFolder;

    /// <summary>True when the type takes launch arguments.</summary>
    public static bool HasArguments(ActionType type) => type is ActionType.LaunchApp or ActionType.RunScript;

    /// <summary>A Segoe Fluent Icons glyph for rows that have no shell icon (services, commands, pauses).</summary>
    public static string Glyph(ActionType type) => type switch
    {
        ActionType.LaunchApp => "\uE71D",
        ActionType.OpenUrl => "\uE774",
        ActionType.OpenFile => "\uE8A5",
        ActionType.OpenFolder => "\uE8B7",
        ActionType.RunScript => "\uE756",
        ActionType.KillProcess => "\uE711",
        ActionType.Delay => "\uE916",
        ActionType.StartService => "\uE912",
        ActionType.StartVpn => "\uE705",
        _ => "\uE71D",
    };

    /// <summary>Short tags for a row's run options; empty when everything is at its default.</summary>
    public static IReadOnlyList<string> Badges(int delaySeconds, FailureBehaviour failure, int retries, bool runAsAdmin)
    {
        var badges = new List<string>();
        if (delaySeconds > 0) badges.Add($"waits {delaySeconds} s");
        if (runAsAdmin) badges.Add("as admin");
        if (failure == FailureBehaviour.Retry) badges.Add(retries == 1 ? "retries once" : $"retries {retries}x");
        if (failure == FailureBehaviour.Stop) badges.Add("stops on failure");
        return badges;
    }
}
