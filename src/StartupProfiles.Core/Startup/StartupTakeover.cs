using System.Security;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Core.Startup;

/// <summary>
/// Makes Startup Profiles the one app that starts at login. <see cref="TakeOver"/> copies every enabled
/// startup app the user can switch off into the Everything profile, then switches it off in Windows
/// (reversibly, like Task Manager) and records it; <see cref="Restore"/> switches the recorded entries back on.
/// </summary>
public sealed class StartupTakeover
{
    private readonly IStartupAppCatalog _catalog;
    private readonly IProfileStore _profiles;
    private readonly IStartupTakeoverStore _record;
    private readonly string _ownRunValueName;

    /// <param name="ownRunValueName">Startup Profiles' own <c>Run</c> value, which is never taken over.</param>
    public StartupTakeover(
        IStartupAppCatalog catalog,
        IProfileStore profiles,
        IStartupTakeoverStore record,
        string ownRunValueName)
    {
        _catalog = catalog;
        _profiles = profiles;
        _record = record;
        _ownRunValueName = ownRunValueName;
    }

    public StartupTakeoverResult TakeOver()
    {
        var enabled = _catalog.GetEntries().Where(e => e.IsEnabled && !IsOwnEntry(e)).ToList();
        var adoptable = enabled.Where(e => e.CanToggle && e.Launch is not null).ToList();
        var leftEnabled = enabled.Where(e => !adoptable.Contains(e)).ToList();

        if (adoptable.Count == 0) return new StartupTakeoverResult([], leftEnabled, []);

        // Capture before switching anything off, so an interrupted run never loses an app.
        AddToEverything(adoptable);
        _record.Save(Merge(_record.Load(), adoptable));

        var adopted = new List<StartupEntry>();
        var failed = new List<StartupEntry>();
        foreach (var entry in adoptable)
        {
            if (TrySetEnabled(entry, enabled: false)) adopted.Add(entry);
            else failed.Add(entry);
        }

        return new StartupTakeoverResult(adopted, leftEnabled, failed);
    }

    /// <summary>True when Startup Profiles has switched startup apps off before (at install or since).</summary>
    public bool HasTakenOver => _record.Load().Count > 0;

    /// <summary>
    /// Records <paramref name="entry"/> (so uninstall switches it back on), then switches it off in Windows. Used for
    /// one app found after install. False when Windows refused; the entry then stays recorded and on.
    /// </summary>
    public bool SwitchOff(StartupEntry entry)
    {
        _record.Save(Merge(_record.Load(), [entry]));
        return TrySetEnabled(entry, enabled: false);
    }

    /// <summary>Switches every recorded entry back on. Entries that fail stay recorded for a later retry.</summary>
    public IReadOnlyList<StartupEntry> Restore()
    {
        var restored = new List<StartupEntry>();
        var remaining = new List<StartupEntry>();
        foreach (var entry in _record.Load())
        {
            if (TrySetEnabled(entry, enabled: true)) restored.Add(entry);
            else remaining.Add(entry);
        }

        if (remaining.Count == 0) _record.Clear();
        else _record.Save(remaining);
        return restored;
    }

    private bool IsOwnEntry(StartupEntry entry) =>
        entry.Source == StartupEntrySource.UserRunKey &&
        string.Equals(entry.Key, _ownRunValueName, StringComparison.OrdinalIgnoreCase);

    private void AddToEverything(IEnumerable<StartupEntry> entries)
    {
        var everything = _profiles.Find(DefaultProfiles.EverythingId) ?? DefaultProfiles.Everything();
        var actions = everything.Actions.ToList();
        foreach (var launch in entries.Select(e => e.Launch!))
        {
            if (!actions.Any(a => SameLaunch(a, launch))) actions.Add(launch);
        }

        _profiles.Save(everything with { Actions = actions });
    }

    private bool TrySetEnabled(StartupEntry entry, bool enabled)
    {
        try
        {
            _catalog.SetEnabled(entry, enabled);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    private static List<StartupEntry> Merge(IReadOnlyList<StartupEntry> existing, IEnumerable<StartupEntry> added) =>
        [.. existing, .. added.Where(a => !existing.Any(e => e.Source == a.Source &&
            string.Equals(e.Key, a.Key, StringComparison.OrdinalIgnoreCase)))];

    private static bool SameLaunch(ProfileAction a, ProfileAction b) =>
        a.Type == b.Type &&
        string.Equals(a.Target, b.Target, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Arguments ?? "", b.Arguments ?? "", StringComparison.OrdinalIgnoreCase);
}
