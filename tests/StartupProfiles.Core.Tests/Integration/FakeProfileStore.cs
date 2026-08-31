using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests.Integration;

/// <summary>In-memory <see cref="IProfileStore"/> so registrar tests stay deterministic and file-free.</summary>
internal sealed class FakeProfileStore : IProfileStore
{
    private readonly List<Profile> _items;

    public FakeProfileStore(params Profile[] seed) => _items = [.. seed];

    public IReadOnlyList<Profile> GetAll() => _items.ToList();

    public Profile? Find(string id) => _items.FirstOrDefault(p => p.Id == id);

    public void Save(Profile profile)
    {
        var index = _items.FindIndex(p => p.Id == profile.Id);
        if (index >= 0) _items[index] = profile;
        else _items.Add(profile);
    }

    public void Remove(string id) => _items.RemoveAll(p => p.Id == id);
}
