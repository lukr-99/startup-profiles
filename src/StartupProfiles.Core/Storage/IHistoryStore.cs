using StartupProfiles.Core.Execution;

namespace StartupProfiles.Core.Storage;

/// <summary>Persistence for recent <see cref="ProfileRun"/> history (history.json), newest first, bounded.</summary>
public interface IHistoryStore
{
    void Append(ProfileRun run);
    IReadOnlyList<ProfileRun> GetRecent(int max = 50);
}
