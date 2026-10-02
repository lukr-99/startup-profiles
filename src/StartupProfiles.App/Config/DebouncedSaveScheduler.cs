using System.Windows.Threading;

namespace StartupProfiles.App.Config;

/// <summary>
/// An <see cref="ISaveScheduler"/> on the UI thread that saves once edits pause for <see cref="Delay"/>.
/// </summary>
public sealed class DebouncedSaveScheduler : ISaveScheduler
{
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer _timer;
    private Action? _pending;

    public DebouncedSaveScheduler()
    {
        _timer = new DispatcherTimer { Interval = Delay };
        _timer.Tick += (_, _) => Flush();
    }

    public void Schedule(Action save)
    {
        _pending = save;
        _timer.Stop();
        _timer.Start();
    }

    public void Flush()
    {
        _timer.Stop();
        var save = _pending;
        _pending = null;
        save?.Invoke();
    }

    public void Cancel()
    {
        _timer.Stop();
        _pending = null;
    }
}
