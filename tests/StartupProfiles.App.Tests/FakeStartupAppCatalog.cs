using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Windows;

namespace StartupProfiles.App.Tests;

/// <summary>In-memory <see cref="IStartupAppCatalog"/> for the config window's startup apps panel.</summary>
internal sealed class FakeStartupAppCatalog : IStartupAppCatalog
{
    public List<StartupEntry> Entries { get; } = [];

    public IReadOnlyList<StartupEntry> GetEntries() => Entries.ToList();

    public void SetEnabled(StartupEntry entry, bool enabled) =>
        throw new NotSupportedException("The config window never switches startup apps.");
}
