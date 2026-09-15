using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Storage;

/// <summary>JSON-file backed <see cref="ILibraryStore"/>. A missing file is an empty library.</summary>
public sealed class LibraryStore : ILibraryStore
{
    private readonly string _file;
    private readonly Lock _gate = new();
    private readonly List<LibraryItem> _items;

    public LibraryStore(string? file = null)
    {
        _file = file ?? StartupProfilesPaths.LibraryFile;
        _items = JsonFile.Load(_file, () => new List<LibraryItem>());
    }

    public IReadOnlyList<LibraryItem> GetAll()
    {
        lock (_gate) return _items.ToList();
    }

    public LibraryItem? Find(string id)
    {
        lock (_gate) return _items.FirstOrDefault(i => i.Id == id);
    }

    public void Save(LibraryItem item)
    {
        lock (_gate)
        {
            var index = _items.FindIndex(i => i.Id == item.Id);
            if (index >= 0) _items[index] = item;
            else _items.Add(item);
            JsonFile.Save(_file, _items);
        }
    }

    public void Remove(string id)
    {
        lock (_gate)
        {
            _items.RemoveAll(i => i.Id == id);
            JsonFile.Save(_file, _items);
        }
    }
}
