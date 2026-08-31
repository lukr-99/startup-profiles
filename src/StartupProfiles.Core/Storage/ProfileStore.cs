using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Storage;

/// <summary>JSON-file backed <see cref="IProfileStore"/>. Seeds <see cref="DefaultProfiles"/> when absent.</summary>
public sealed class ProfileStore : IProfileStore
{
    private readonly string _file;
    private readonly Lock _gate = new();
    private readonly List<Profile> _items;

    public ProfileStore(string? file = null)
    {
        _file = file ?? StartupProfilesPaths.ProfilesFile;
        _items = JsonFile.Load(_file, () => new List<Profile>(DefaultProfiles.Create()));
    }

    public IReadOnlyList<Profile> GetAll()
    {
        lock (_gate) return _items.ToList();
    }

    public Profile? Find(string id)
    {
        lock (_gate) return _items.FirstOrDefault(p => p.Id == id);
    }

    public void Save(Profile profile)
    {
        lock (_gate)
        {
            var index = _items.FindIndex(p => p.Id == profile.Id);
            if (index >= 0) _items[index] = profile;
            else _items.Add(profile);
            JsonFile.Save(_file, _items);
        }
    }

    public void Remove(string id)
    {
        lock (_gate)
        {
            _items.RemoveAll(p => p.Id == id);
            JsonFile.Save(_file, _items);
        }
    }
}
