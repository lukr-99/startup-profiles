using System.Collections.ObjectModel;
using System.Windows.Input;
using StartupProfiles.App.Mvvm;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Launcher;

/// <summary>Drives the login selector: lists profiles and runs the chosen one.</summary>
public sealed class LauncherViewModel : ObservableObject
{
    private readonly IProfileStore _profiles;
    private readonly ProfileExecutor _executor;
    private string? _status;

    public LauncherViewModel(IProfileStore profiles, ProfileExecutor executor)
    {
        _profiles = profiles;
        _executor = executor;

        RunCommand = new RelayCommand(p => { if (p is ProfileTile tile) _ = RunAsync(tile); });
        EditCommand = new RelayCommand(_ => OpenConfigRequested?.Invoke());
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());

        Load();
    }

    public ObservableCollection<ProfileTile> Profiles { get; } = [];

    public ICommand RunCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CloseCommand { get; }

    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    public event Action? CloseRequested;
    public event Action? OpenConfigRequested;

    public void Load()
    {
        Profiles.Clear();
        foreach (var profile in _profiles.GetAll()) Profiles.Add(ProfileTile.From(profile));
    }

    public async Task RunAsync(ProfileTile tile)
    {
        Status = $"Launching {tile.Name}...";
        if (_profiles.Find(tile.Id) is { } profile) await _executor.RunAndRecordAsync(profile);
        CloseRequested?.Invoke();
    }
}
