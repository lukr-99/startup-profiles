using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;

namespace StartupProfiles.App.Config;

/// <summary>
/// A Windows startup app in the config window's side panel, ready to drag (or double-click) into the actions
/// table of the profile or base being edited.
/// </summary>
public sealed record StartupAppItem(string Name, string Detail, bool StartsWithWindows, ProfileAction Launch)
{
    public static StartupAppItem From(StartupEntry entry, ProfileAction launch) => new(
        entry.Name,
        Describe(entry),
        entry.IsEnabled,
        launch);

    private static string Describe(StartupEntry entry)
    {
        var source = entry.Source switch
        {
            StartupEntrySource.UserStartupFolder or StartupEntrySource.CommonStartupFolder => "Startup folder",
            StartupEntrySource.PackagedTask => "Store app",
            _ => "Registry",
        };
        if (!entry.CanToggle) source += ", all users";
        return entry.IsEnabled ? $"{source} · starts with Windows" : $"{source} · off in Windows";
    }
}
