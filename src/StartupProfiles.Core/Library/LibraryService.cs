using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Library;

/// <summary>
/// The global library of startable items, kept consistent with the profile and base actions that link to them:
/// new items get unique ids, and deleting an item turns its links back into standalone actions.
/// </summary>
public sealed class LibraryService
{
    private readonly ILibraryStore _library;
    private readonly IProfileStore _profiles;
    private readonly IBaseStore _base;

    public LibraryService(ILibraryStore library, IProfileStore profiles, IBaseStore baseStore)
    {
        _library = library;
        _profiles = profiles;
        _base = baseStore;
    }

    public IReadOnlyList<LibraryItem> GetAll() => _library.GetAll();

    public LibraryItem? Find(string id) => _library.Find(id);

    /// <summary>The item that starts the same thing as <paramref name="action"/>, adding one named <paramref name="name"/> if none does.</summary>
    public LibraryItem FindOrAdd(string name, ProfileAction action) =>
        _library.GetAll().FirstOrDefault(i => i.Starts(action)) ?? Add(new LibraryItem
        {
            Id = "",
            Name = name,
            Type = action.Type,
            Target = action.Target,
            Arguments = string.IsNullOrEmpty(action.Arguments) ? null : action.Arguments,
            RunAsAdmin = action.RunAsAdmin,
        });

    /// <summary>Saves <paramref name="item"/> as a new entry under a fresh id derived from its name.</summary>
    public LibraryItem Add(LibraryItem item)
    {
        var added = item with { Id = NewId(item.Name) };
        _library.Save(added);
        return added;
    }

    /// <summary>Updates an existing item; every action linked to it starts the new values from now on.</summary>
    public void Save(LibraryItem item) => _library.Save(item);

    /// <summary>Names of the profiles (with "Base" first) that have an action linked to the item.</summary>
    public IReadOnlyList<string> UsedBy(string id)
    {
        var users = _profiles.GetAll().Where(p => Links(p.Actions, id)).Select(p => p.Name).ToList();
        if (Links(_base.Load().Actions, id)) users.Insert(0, "Base");
        return users;
    }

    /// <summary>
    /// Deletes the item. Actions that linked to it keep its current values as standalone actions, so the profiles
    /// that used it still start the same thing.
    /// </summary>
    public void Remove(string id)
    {
        if (_library.Find(id) is not { } item) return;

        foreach (var profile in _profiles.GetAll().Where(p => Links(p.Actions, id)))
            _profiles.Save(profile with { Actions = Unlink(profile.Actions, item) });

        var baseValue = _base.Load();
        if (Links(baseValue.Actions, id)) _base.Save(baseValue with { Actions = Unlink(baseValue.Actions, item) });

        _library.Remove(id);
    }

    private static bool Links(IEnumerable<ProfileAction> actions, string id) =>
        actions.Any(a => a.LibraryItemId == id);

    private static List<ProfileAction> Unlink(IEnumerable<ProfileAction> actions, LibraryItem item) =>
        [.. actions.Select(a => a.LibraryItemId == item.Id ? item.ApplyTo(a) with { LibraryItemId = null } : a)];

    private string NewId(string name)
    {
        var chars = name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length == 0) slug = "item";

        var taken = _library.GetAll().Select(i => i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var id = slug;
        for (var n = 2; taken.Contains(id); n++) id = $"{slug}-{n}";
        return id;
    }
}
