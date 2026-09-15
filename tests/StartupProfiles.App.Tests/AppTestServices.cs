using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Library;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

/// <summary>Builds Core services over throwaway temp files for view-model tests.</summary>
internal sealed class AppTestServices : IDisposable
{
    private readonly string[] _files;

    private AppTestServices(string[] files, string historyFile, IProfileStore profiles, IBaseStore baseStore,
        LibraryService library, ProfileExecutor executor)
    {
        _files = files;
        HistoryFile = historyFile;
        Profiles = profiles;
        Base = baseStore;
        Library = library;
        Executor = executor;
    }

    public IProfileStore Profiles { get; }
    public IBaseStore Base { get; }
    public LibraryService Library { get; }
    public FakeStartupAppCatalog StartupApps { get; } = new();
    public ProfileExecutor Executor { get; }
    public string HistoryFile { get; }

    public static AppTestServices Create()
    {
        string TempFile(string prefix) => Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.json");

        var profilesFile = TempFile("sp-vm");
        var historyFile = TempFile("sp-vmh");
        var baseFile = TempFile("sp-vmb");
        var libraryFile = TempFile("sp-vml");

        var profiles = new ProfileStore(profilesFile);
        var baseStore = new BaseStore(baseFile);
        var libraryStore = new LibraryStore(libraryFile);
        var runner = new ProfileRunner(ActionHandlerRegistry.CreateDefault(new SystemProcessLauncher()), new TaskDelayer());
        return new AppTestServices(
            [profilesFile, historyFile, baseFile, libraryFile],
            historyFile,
            profiles,
            baseStore,
            new LibraryService(libraryStore, profiles, baseStore),
            new ProfileExecutor(runner, new HistoryStore(historyFile), baseStore, libraryStore));
    }

    public void Dispose()
    {
        foreach (var file in _files) File.Delete(file);
    }
}
