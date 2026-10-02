using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.App.Tests;

/// <summary>In-memory <see cref="IStartupAppCatalog"/> for the startup apps panel and the new-apps window.</summary>
internal sealed class FakeStartupAppCatalog : IStartupAppCatalog
{
    public List<StartupEntry> Entries { get; } = [];

    public IReadOnlyList<StartupEntry> GetEntries() => Entries.ToList();

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        var index = Entries.FindIndex(e => e.Source == entry.Source && e.Key == entry.Key);
        if (index >= 0) Entries[index] = Entries[index] with { IsEnabled = enabled };
    }
}
