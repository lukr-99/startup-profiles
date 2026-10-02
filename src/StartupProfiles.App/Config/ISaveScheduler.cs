namespace StartupProfiles.App.Config;

/// <summary>
/// Decides when an edit is written. The window waits a moment so typing a name is one save, not one per key;
/// tests save at once. Only the latest scheduled save is kept.
/// </summary>
public interface ISaveScheduler
{
    /// <summary>Runs <paramref name="save"/> later, replacing any save still waiting.</summary>
    void Schedule(Action save);

    /// <summary>Runs the waiting save now, if there is one.</summary>
    void Flush();

    /// <summary>Drops the waiting save without running it.</summary>
    void Cancel();
}
