namespace StartupProfiles.Core.Storage;

/// <summary>JSON-file backed <see cref="IStartupSeenStore"/>.</summary>
public sealed class StartupSeenStore : IStartupSeenStore
{
    private readonly string _file;

    public StartupSeenStore(string? file = null) => _file = file ?? StartupProfilesPaths.StartupSeenFile;

    public IReadOnlySet<string>? Load() =>
        File.Exists(_file)
            ? JsonFile.Load(_file, () => new List<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

    public void Save(IEnumerable<string> keys) =>
        JsonFile.Save(_file, keys.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList());
}
