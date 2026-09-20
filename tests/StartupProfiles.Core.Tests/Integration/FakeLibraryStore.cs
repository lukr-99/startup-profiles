using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests.Integration;

/// <summary>In-memory <see cref="ILibraryStore"/> so registrar tests stay deterministic and file-free.</summary>
internal sealed class FakeLibraryStore : ILibraryStore
{
    private readonly List<LibraryItem> _items;

    public FakeLibraryStore(params LibraryItem[] seed) => _items = [.. seed];

    public IReadOnlyList<LibraryItem> GetAll() => _items.ToList();

    public LibraryItem? Find(string id) => _items.FirstOrDefault(i => i.Id == id);

    public void Save(LibraryItem item)
    {
        var index = _items.FindIndex(i => i.Id == item.Id);
        if (index >= 0) _items[index] = item;
        else _items.Add(item);
    }

    public void Remove(string id) => _items.RemoveAll(i => i.Id == id);
}
