using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Storage;

/// <summary>JSON-file backed <see cref="IBaseStore"/>. A missing file is an empty base.</summary>
public sealed class BaseStore : IBaseStore
{
    private readonly string _file;
    private readonly Lock _gate = new();
    private StartupBase _value;

    public BaseStore(string? file = null)
    {
        _file = file ?? StartupProfilesPaths.BaseFile;
        _value = JsonFile.Load(_file, () => new StartupBase());
    }

    public StartupBase Load()
    {
        lock (_gate) return _value;
    }

    public void Save(StartupBase value)
    {
        lock (_gate)
        {
            _value = value;
            JsonFile.Save(_file, value);
        }
    }
}
