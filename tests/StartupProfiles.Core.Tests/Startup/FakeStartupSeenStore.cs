using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests.Startup;

/// <summary>In-memory <see cref="IStartupSeenStore"/>; null <see cref="Keys"/> means nothing was ever recorded.</summary>
internal sealed class FakeStartupSeenStore : IStartupSeenStore
{
    public HashSet<string>? Keys { get; set; }

    public IReadOnlySet<string>? Load() => Keys?.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Save(IEnumerable<string> keys) => Keys = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
}
