using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Storage;

/// <summary>Persistence for the global library of startable <see cref="LibraryItem"/>s (library.json).</summary>
public interface ILibraryStore
{
    IReadOnlyList<LibraryItem> GetAll();
    LibraryItem? Find(string id);
    void Save(LibraryItem item);
    void Remove(string id);
}
