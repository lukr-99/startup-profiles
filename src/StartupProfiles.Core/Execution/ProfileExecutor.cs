using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Execution;

/// <summary>
/// Runs a profile and records the resulting <see cref="ProfileRun"/> to history. Single source of truth
/// for "run then record", shared by the launcher, the config window, the API's run endpoint, and the tray -
/// so it is also where the <see cref="StartupBase"/> is applied: a profile that includes the base runs the
/// base actions first, as one run.
/// </summary>
public sealed class ProfileExecutor
{
    /// <summary>The profile id a run of the base on its own is recorded under.</summary>
    public const string BaseRunId = "base";

    private readonly ProfileRunner _runner;
    private readonly IHistoryStore _history;
    private readonly IBaseStore _base;

    public ProfileExecutor(ProfileRunner runner, IHistoryStore history, IBaseStore baseStore)
    {
        _runner = runner;
        _history = history;
        _base = baseStore;
    }

    public Task<ProfileRun> RunAndRecordAsync(Profile profile, CancellationToken ct = default) =>
        RecordAsync(WithBase(profile), ct);

    /// <summary>Runs only the base actions (the base editor's "Run now").</summary>
    public Task<ProfileRun> RunBaseAndRecordAsync(CancellationToken ct = default) =>
        RecordAsync(new Profile { Id = BaseRunId, Name = "Base", IncludeBase = false, Actions = _base.Load().Actions }, ct);

    private Profile WithBase(Profile profile)
    {
        if (!profile.IncludeBase) return profile;

        var baseActions = _base.Load().Actions;
        return baseActions.Count == 0 ? profile : profile with { Actions = [.. baseActions, .. profile.Actions] };
    }

    private async Task<ProfileRun> RecordAsync(Profile profile, CancellationToken ct)
    {
        var run = await _runner.RunAsync(profile, ct).ConfigureAwait(false);
        _history.Append(run);
        return run;
    }
}
