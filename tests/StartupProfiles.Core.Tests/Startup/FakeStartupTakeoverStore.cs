using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests.Startup;

/// <summary>In-memory <see cref="IStartupTakeoverStore"/> so takeover tests stay file-free.</summary>
internal sealed class FakeStartupTakeoverStore : IStartupTakeoverStore
{
    public List<StartupEntry> Entries { get; private set; } = [];

    public IReadOnlyList<StartupEntry> Load() => Entries.ToList();

    public void Save(IReadOnlyList<StartupEntry> entries) => Entries = [.. entries];

    public void Clear() => Entries = [];
}
