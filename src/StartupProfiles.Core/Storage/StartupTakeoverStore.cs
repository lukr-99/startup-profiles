using StartupProfiles.Core.Startup;

namespace StartupProfiles.Core.Storage;

/// <summary>JSON-file backed <see cref="IStartupTakeoverStore"/>.</summary>
public sealed class StartupTakeoverStore : IStartupTakeoverStore
{
    private readonly string _file;

    public StartupTakeoverStore(string? file = null) => _file = file ?? StartupProfilesPaths.StartupTakeoverFile;

    public IReadOnlyList<StartupEntry> Load() => JsonFile.Load(_file, () => new List<StartupEntry>());

    public void Save(IReadOnlyList<StartupEntry> entries) => JsonFile.Save(_file, entries);

    public void Clear() => File.Delete(_file);
}
