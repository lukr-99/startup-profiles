using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.Core.Tests.Startup;

/// <summary>In-memory <see cref="IStartupAppCatalog"/>; <see cref="FailingKeys"/> simulates access-denied switches.</summary>
internal sealed class FakeStartupAppCatalog : IStartupAppCatalog
{
    private readonly List<StartupEntry> _entries;

    public FakeStartupAppCatalog(params StartupEntry[] entries) => _entries = [.. entries];

    public HashSet<string> FailingKeys { get; } = [];

    public IReadOnlyList<StartupEntry> GetEntries() => _entries.ToList();

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        if (FailingKeys.Contains(entry.Key)) throw new UnauthorizedAccessException($"Access to '{entry.Key}' denied.");

        var index = _entries.FindIndex(e => e.Source == entry.Source && e.Key == entry.Key);
        if (index >= 0) _entries[index] = _entries[index] with { IsEnabled = enabled };
    }

    public StartupEntry Get(string key) => _entries.Single(e => e.Key == key);
}
