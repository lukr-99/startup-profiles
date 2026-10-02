using StartupProfiles.App.Config;

namespace StartupProfiles.App.Tests;

/// <summary>An <see cref="ISaveScheduler"/> that holds the save until the test flushes it, like the window's pause.</summary>
internal sealed class ManualSaveScheduler : ISaveScheduler
{
    private Action? _pending;

    public bool HasPending => _pending is not null;

    public void Schedule(Action save) => _pending = save;

    public void Flush()
    {
        var save = _pending;
        _pending = null;
        save?.Invoke();
    }

    public void Cancel() => _pending = null;
}
