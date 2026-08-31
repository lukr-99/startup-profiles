using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Execution;

/// <summary>
/// Runs a profile and records the resulting <see cref="ProfileRun"/> to history. Single source of truth
/// for "run then record", shared by the API's run endpoint and the tray.
/// </summary>
public sealed class ProfileExecutor
{
    private readonly ProfileRunner _runner;
    private readonly IHistoryStore _history;

    public ProfileExecutor(ProfileRunner runner, IHistoryStore history)
    {
        _runner = runner;
        _history = history;
    }

    public async Task<ProfileRun> RunAndRecordAsync(Profile profile, CancellationToken ct = default)
    {
        var run = await _runner.RunAsync(profile, ct).ConfigureAwait(false);
        _history.Append(run);
        return run;
    }
}
