using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

/// <summary>A history store that fails to record, so a profile run throws after its actions ran.</summary>
internal sealed class ThrowingHistoryStore : IHistoryStore
{
    public void Append(ProfileRun run) => throw new IOException("history.json is locked");

    public IReadOnlyList<ProfileRun> GetRecent(int max = 50) => [];
}
