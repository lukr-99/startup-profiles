namespace StartupProfiles.App.Config;

/// <summary>An <see cref="ISaveScheduler"/> that saves right away. The default when none is given.</summary>
public sealed class ImmediateSaveScheduler : ISaveScheduler
{
    public void Schedule(Action save) => save();

    public void Flush() { }

    public void Cancel() { }
}
