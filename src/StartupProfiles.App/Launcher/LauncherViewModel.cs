using System.Collections.ObjectModel;
using System.Windows.Input;
using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Launcher;

/// <summary>
/// Drives the login selector: greets the user, lists profiles with the last one used pre-selected, and runs the
/// chosen one. When opened at login, a profile set to start by itself (see <see cref="StartupBehaviour"/>) gets a
/// countdown the user can cancel; the window calls <see cref="Tick"/> once a second.
/// </summary>
public sealed class LauncherViewModel : ObservableObject
{
    /// <summary>How long the launcher waits before starting a profile by itself.</summary>
    public const int CountdownSeconds = 10;

    private readonly IProfileStore _profiles;
    private readonly ProfileExecutor _executor;
    private readonly IHistoryStore? _history;
    private readonly Func<DateTime> _now;
    private readonly RelayCommand _cancelCountdownCommand;
    private string? _status;
    private ProfileTile? _selected;
    private ProfileTile? _autoStart;
    private int _secondsLeft;
    private bool _running;

    /// <param name="history">Where the last run is read from; without it nothing is pre-selected.</param>
    /// <param name="now">The clock for the greeting; the system clock when omitted.</param>
    /// <param name="atLogin">True when shown at login, the only time a profile may start by itself.</param>
    public LauncherViewModel(IProfileStore profiles, ProfileExecutor executor, IHistoryStore? history = null,
        Func<DateTime>? now = null, bool atLogin = false)
    {
        _profiles = profiles;
        _executor = executor;
        _history = history;
        _now = now ?? (() => DateTime.Now);

        RunCommand = new RelayCommand(p => { if (p is ProfileTile tile) _ = RunAsync(tile); });
        RunSelectedCommand = new RelayCommand(_ => { if (Selected is { } tile) _ = RunAsync(tile); });
        EditCommand = new RelayCommand(_ => { CancelCountdown(); OpenConfigRequested?.Invoke(); });
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
        _cancelCountdownCommand = new RelayCommand(_ => CancelCountdown(), _ => IsCountingDown);

        Load();
        if (atLogin) StartCountdown();
    }

    public ObservableCollection<ProfileTile> Profiles { get; } = [];

    public ICommand RunCommand { get; }

    /// <summary>Runs <see cref="Selected"/> (the Enter key).</summary>
    public ICommand RunSelectedCommand { get; }

    public ICommand EditCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand CancelCountdownCommand => _cancelCountdownCommand;

    /// <summary>"Good morning" and friends, by the time of day.</summary>
    public string Greeting => _now().Hour switch
    {
        >= 5 and < 12 => "Good morning",
        >= 12 and < 18 => "Good afternoon",
        >= 18 and < 23 => "Good evening",
        _ => "Hello",
    };

    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    /// <summary>The tile Enter starts: the profile that ran last, until the user picks another.</summary>
    public ProfileTile? Selected { get => _selected; set => SetProperty(ref _selected, value); }

    /// <summary>The profile that starts when the countdown ends, or null when nothing is counting down.</summary>
    public ProfileTile? AutoStart
    {
        get => _autoStart;
        private set
        {
            if (!SetProperty(ref _autoStart, value)) return;
            OnPropertyChanged(nameof(IsCountingDown));
            OnPropertyChanged(nameof(CountdownText));
            _cancelCountdownCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsCountingDown => AutoStart is not null;

    public int SecondsLeft
    {
        get => _secondsLeft;
        private set { if (SetProperty(ref _secondsLeft, value)) OnPropertyChanged(nameof(CountdownText)); }
    }

    public string? CountdownText => AutoStart is { } tile ? $"Starting {tile.Name} in {SecondsLeft} s" : null;


    public event Action? CloseRequested;
    public event Action? OpenConfigRequested;

    public void Load()
    {
        var lastId = LastProfileId();
        Profiles.Clear();
        foreach (var (profile, index) in _profiles.GetAll().Select((p, i) => (p, i)))
            Profiles.Add(ProfileTile.From(profile, index, profile.Id == lastId));
        Selected = Profiles.FirstOrDefault(t => t.IsLast) ?? Profiles.FirstOrDefault();
    }

    /// <summary>Counts one second down; starts the profile when it reaches zero.</summary>
    public void Tick()
    {
        if (AutoStart is not { } tile) return;
        if (SecondsLeft > 1)
        {
            SecondsLeft--;
            return;
        }

        AutoStart = null;
        _ = RunAsync(tile);
    }

    /// <summary>Stops the countdown; the user is choosing.</summary>
    public void CancelCountdown() => AutoStart = null;

    public async Task RunAsync(ProfileTile tile)
    {
        if (_running) return;
        _running = true;
        CancelCountdown();
        Status = $"Starting {tile.Name}...";
        if (_profiles.Find(tile.Id) is { } profile) await _executor.RunAndRecordAsync(profile);
        CloseRequested?.Invoke();
    }

    private void StartCountdown()
    {
        // The last-used profile wins if it may start by itself; otherwise the first one set to always start.
        var last = Profiles.FirstOrDefault(t => t.IsLast);
        AutoStart = last is { StartupBehaviour: StartupBehaviour.RememberLast or StartupBehaviour.AutoSelectAfterTimeout }
            ? last
            : Profiles.FirstOrDefault(t => t.StartupBehaviour == StartupBehaviour.AutoSelectAfterTimeout);
        if (AutoStart is null) return;

        SecondsLeft = CountdownSeconds;
        Selected = AutoStart;
    }

    private string? LastProfileId() =>
        _history?.GetRecent(20).FirstOrDefault(r => r.ProfileId != ProfileExecutor.BaseRunId)?.ProfileId;
}
