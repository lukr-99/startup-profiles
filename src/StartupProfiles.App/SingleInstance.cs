using System.Runtime.InteropServices;

namespace StartupProfiles.App;

/// <summary>
/// One full instance per user session. A later launch (Start Menu, shortcut, the exe) must not start a second
/// host, so instead of exiting silently it signals the running instance, which shows its launcher. The names
/// are injectable so tests use their own kernel objects.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string DefaultName = "StartupProfiles.Local.SingleInstance";
    private const int AsfwAny = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showSignal;
    private RegisteredWaitHandle? _listener;

    public SingleInstance(string name = DefaultName)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsFirst = createdNew;
        _showSignal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, name + ".ShowLauncher");
    }

    /// <summary>True if this process owns the instance; false if another one is already running.</summary>
    public bool IsFirst { get; }

    /// <summary>From a later launch: asks the running instance to show its launcher.</summary>
    public void SignalFirst()
    {
        // The launched process holds the foreground right; pass it on so the running instance's window can
        // come to the front instead of flashing in the taskbar.
        _ = AllowSetForegroundWindow(AsfwAny);
        _showSignal.Set();
    }

    /// <summary>
    /// In the running instance: invokes <paramref name="onShow"/> on a thread-pool thread for every later
    /// launch, including one that signalled before this was called.
    /// </summary>
    public void Listen(Action onShow)
    {
        _listener?.Unregister(null);
        _listener = ThreadPool.RegisterWaitForSingleObject(
            _showSignal, (_, _) => onShow(), state: null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _listener?.Unregister(null);
        _showSignal.Dispose();
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
