using StartupProfiles.Core.Library;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Core.Startup;

/// <summary>
/// Finds apps that set themselves to start with Windows since Startup Profiles last looked, and places each where
/// the user chose. The first look only records what is there (install already handled those apps), so later looks
/// report just the new ones. Every entry is offered once: it is marked seen whatever the user decides.
/// </summary>
public sealed class StartupDiscovery
{
    private readonly IStartupAppCatalog _catalog;
    private readonly IStartupSeenStore _seen;
    private readonly StartupTakeover _takeover;
    private readonly IProfileStore _profiles;
    private readonly IBaseStore _base;
    private readonly LibraryService _library;
    private readonly string _ownRunValueName;

    /// <param name="ownRunValueName">Startup Profiles' own <c>Run</c> value, which is never offered.</param>
    public StartupDiscovery(
        IStartupAppCatalog catalog,
        IStartupSeenStore seen,
        StartupTakeover takeover,
        IProfileStore profiles,
        IBaseStore baseStore,
        LibraryService library,
        string ownRunValueName)
    {
        _catalog = catalog;
        _seen = seen;
        _takeover = takeover;
        _profiles = profiles;
        _base = baseStore;
        _library = library;
        _ownRunValueName = ownRunValueName;
    }

    /// <summary>
    /// What a new app should default to: the Everything profile when startup apps were taken over before (the user
    /// chose that at install), otherwise leave it to Windows.
    /// </summary>
    public StartupPlacement SuggestedPlacement =>
        _takeover.HasTakenOver && _profiles.Find(DefaultProfiles.EverythingId) is not null
            ? StartupPlacement.Profile(DefaultProfiles.EverythingId)
            : StartupPlacement.LeaveInWindows;

    /// <summary>
    /// Enabled startup apps the user can switch off and a profile can start, which were not there last time.
    /// The first call records everything and returns nothing.
    /// </summary>
    public IReadOnlyList<StartupEntry> FindNew()
    {
        var entries = _catalog.GetEntries();
        var seen = _seen.Load();
        if (seen is null)
        {
            _seen.Save(entries.Select(Key));
            return [];
        }

        return entries
            .Where(e => e is { IsEnabled: true, CanToggle: true, Launch: not null } && !IsOwnEntry(e) && !seen.Contains(Key(e)))
            .ToList();
    }

    /// <summary>Remembers <paramref name="entries"/> so they are not offered again.</summary>
    public void MarkSeen(IEnumerable<StartupEntry> entries) =>
        _seen.Save([.. _seen.Load() ?? new HashSet<string>(), .. entries.Select(Key)]);

    /// <summary>
    /// Places <paramref name="entry"/> and marks it seen. For a profile or the base it is kept in the library, added
    /// there as a linked step (once), and switched off in Windows. Returns false only when Windows refused to switch
    /// it off.
    /// </summary>
    public bool Place(StartupEntry entry, StartupPlacement placement)
    {
        MarkSeen([entry]);
        if (placement.Kind == StartupPlacementKind.LeaveInWindows || entry.Launch is not { } launch) return true;

        var item = _library.FindOrAdd(entry.Name, launch);
        if (placement.Kind == StartupPlacementKind.LibraryOnly) return true;

        var step = item.ApplyTo(launch);
        if (placement.Kind == StartupPlacementKind.Base)
        {
            var value = _base.Load();
            if (!Contains(value.Actions, item)) _base.Save(value with { Actions = [.. value.Actions, step] });
        }
        else if (placement.ProfileId is { } id && _profiles.Find(id) is { } profile)
        {
            if (!Contains(profile.Actions, item)) _profiles.Save(profile with { Actions = [.. profile.Actions, step] });
        }
        else
        {
            return true; // The profile is gone; the app stays in the library and on in Windows.
        }

        return _takeover.SwitchOff(entry);
    }

    /// <summary>The key an entry is remembered by: its source and its key there.</summary>
    public static string Key(StartupEntry entry) => $"{entry.Source}|{entry.Key}";

    private static bool Contains(IEnumerable<ProfileAction> actions, LibraryItem item) =>
        actions.Any(a => a.LibraryItemId == item.Id || item.Starts(a));

    private bool IsOwnEntry(StartupEntry entry) =>
        entry.Source == StartupEntrySource.UserRunKey &&
        string.Equals(entry.Key, _ownRunValueName, StringComparison.OrdinalIgnoreCase);
}
