using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

/// <summary>Builds Core services over throwaway temp files for view-model tests.</summary>
internal sealed class AppTestServices : IDisposable
{
    private AppTestServices(string profilesFile, string historyFile, IProfileStore profiles, ProfileExecutor executor)
    {
        _profilesFile = profilesFile;
        _historyFile = historyFile;
        Profiles = profiles;
        Executor = executor;
    }

    private readonly string _profilesFile;
    private readonly string _historyFile;

    public IProfileStore Profiles { get; }
    public ProfileExecutor Executor { get; }
    public string HistoryFile => _historyFile;
    public string ProfilesFile => _profilesFile;

    public static AppTestServices Create()
    {
        var profilesFile = Path.Combine(Path.GetTempPath(), $"sp-vm-{Guid.NewGuid():N}.json");
        var historyFile = Path.Combine(Path.GetTempPath(), $"sp-vmh-{Guid.NewGuid():N}.json");
        var profiles = new ProfileStore(profilesFile);
        var runner = new ProfileRunner(ActionHandlerRegistry.CreateDefault(new SystemProcessLauncher()), new TaskDelayer());
        return new AppTestServices(profilesFile, historyFile, profiles, new ProfileExecutor(runner, new HistoryStore(historyFile)));
    }

    public void Dispose()
    {
        File.Delete(_profilesFile);
        File.Delete(_historyFile);
    }
}
