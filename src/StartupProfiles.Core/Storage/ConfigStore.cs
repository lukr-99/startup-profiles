namespace StartupProfiles.Core.Storage;

/// <summary>JSON-file backed <see cref="IConfigStore"/> (config.json).</summary>
public sealed class ConfigStore : IConfigStore
{
    private readonly string _file;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _items;

    public ConfigStore(string? file = null)
    {
        _file = file ?? StartupProfilesPaths.ConfigFile;
        _items = JsonFile.Load(_file, () => new Dictionary<string, string>());
    }

    public string? GetValue(string key)
    {
        lock (_gate) return _items.TryGetValue(key, out var value) ? value : null;
    }

    public string GetValueOrDefault(string key, string fallback) => GetValue(key) ?? fallback;

    public void SetValue(string key, string? value)
    {
        lock (_gate)
        {
            if (value is null) _items.Remove(key);
            else _items[key] = value;
            JsonFile.Save(_file, _items);
        }
    }

    public IReadOnlyDictionary<string, string> GetAll()
    {
        lock (_gate) return new Dictionary<string, string>(_items);
    }
}
