using Microsoft.Win32;

namespace StartupProfiles.App.Themes;

/// <summary>
/// Raises <see cref="Changed"/> when the Windows "apps use light theme" setting flips while the app
/// runs. It listens to <see cref="SystemEvents.UserPreferenceChanged"/> only between
/// <see cref="Start"/> and <see cref="Stop"/>, and ignores preference changes that leave the setting
/// as it was. <see cref="Changed"/> is raised on the system events thread, not the UI thread.
/// </summary>
public sealed class SystemThemeWatcher
{
    private readonly Func<bool> _readIsDark;
    private readonly object _gate = new();
    private bool _isDark;
    private bool _listening;

    /// <param name="readIsDark">Reads whether Windows currently asks apps to use the dark theme.</param>
    public SystemThemeWatcher(Func<bool> readIsDark) => _readIsDark = readIsDark;

    public event EventHandler? Changed;

    public bool IsListening
    {
        get { lock (_gate) return _listening; }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_listening) return;
            _isDark = _readIsDark();
            _listening = true;
        }
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_listening) return;
            _listening = false;
        }
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    /// <summary>
    /// Handles one preference change. The light/dark setting is reported under
    /// <see cref="UserPreferenceCategory.General"/>.
    /// </summary>
    public void Handle(UserPreferenceCategory category)
    {
        if (category != UserPreferenceCategory.General) return;
        lock (_gate)
        {
            if (!_listening) return;
            var isDark = _readIsDark();
            if (isDark == _isDark) return;
            _isDark = isDark;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e) => Handle(e.Category);
}
