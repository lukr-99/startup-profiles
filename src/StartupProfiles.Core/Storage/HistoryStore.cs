using StartupProfiles.Core.Execution;

namespace StartupProfiles.Core.Storage;

/// <summary>JSON-file backed <see cref="IHistoryStore"/>. Keeps the most recent runs, newest first.</summary>
public sealed class HistoryStore : IHistoryStore
{
    private const int MaxEntries = 200;

    private readonly string _file;
    private readonly Lock _gate = new();
    private readonly List<ProfileRun> _items;

    public HistoryStore(string? file = null)
    {
        _file = file ?? StartupProfilesPaths.HistoryFile;
        _items = JsonFile.Load(_file, () => new List<ProfileRun>());
    }

    public void Append(ProfileRun run)
    {
        lock (_gate)
        {
            _items.Insert(0, run);
            if (_items.Count > MaxEntries) _items.RemoveRange(MaxEntries, _items.Count - MaxEntries);
            JsonFile.Save(_file, _items);
        }
    }

    public IReadOnlyList<ProfileRun> GetRecent(int max = 50)
    {
        lock (_gate) return _items.Take(max).ToList();
    }
}
