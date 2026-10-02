using StartupProfiles.App.Config;
using StartupProfiles.Core.Backup;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

public sealed class BackupFlowTests : IDisposable
{
    private readonly string _backupFile = Path.Combine(Path.GetTempPath(), $"sp-bk-{Guid.NewGuid():N}.json");
    private readonly string _configFile = Path.Combine(Path.GetTempPath(), $"sp-bkc-{Guid.NewGuid():N}.json");
    private readonly string _libraryFile = Path.Combine(Path.GetTempPath(), $"sp-bkl-{Guid.NewGuid():N}.json");
    private readonly AppTestServices _services = AppTestServices.Create();

    public void Dispose()
    {
        File.Delete(_backupFile);
        File.Delete(_configFile);
        File.Delete(_libraryFile);
        _services.Dispose();
    }

    private ConfigViewModel Open(FakeUserPrompts prompts)
    {
        var config = new ConfigStore(_configFile);
        var backup = new BackupService(_services.Profiles, _services.Base, new LibraryStore(_libraryFile), config, "0.1.0");
        return new ConfigViewModel(_services.Profiles, _services.Base, _services.Library, _services.StartupApps,
            _services.Executor, prompts, config: config, backup: backup);
    }

    [Fact]
    public void BackUp_ThenRestore_AfterAsking_BringsTheProfileBack()
    {
        var prompts = new FakeUserPrompts { SavePathResult = _backupFile, OpenPathResult = _backupFile, ConfirmResult = true };
        var viewModel = Open(prompts);
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "work");
        viewModel.Editor!.Actions.Add(new ActionEditor { Type = ActionType.OpenUrl, Target = "https://example.com" });

        viewModel.ExportCommand.Execute(null);
        _services.Profiles.Save(_services.Profiles.Find("work")! with { Actions = [] });
        viewModel.ImportCommand.Execute(null);

        Assert.Single(_services.Profiles.Find("work")!.Actions);
        Assert.Null(viewModel.Editor);
        Assert.StartsWith("Restored", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_WhenTheUserSaysNo_ChangesNothing()
    {
        var prompts = new FakeUserPrompts { SavePathResult = _backupFile, OpenPathResult = _backupFile };
        var viewModel = Open(prompts);
        viewModel.ExportCommand.Execute(null);
        _services.Profiles.Save(new Profile { Id = "new-one", Name = "New one" });

        prompts.ConfirmResult = false;
        viewModel.ImportCommand.Execute(null);

        Assert.NotNull(_services.Profiles.Find("new-one"));
    }

    [Fact]
    public void ABadFile_IsExplained_AndNothingChanges()
    {
        File.WriteAllText(_backupFile, """{ "Format": "other" }""");
        var prompts = new FakeUserPrompts { OpenPathResult = _backupFile };
        var viewModel = Open(prompts);

        viewModel.ImportCommand.Execute(null);

        Assert.Contains(prompts.Infos, m => m.Contains("not a Startup Profiles backup", StringComparison.Ordinal));
        Assert.NotNull(_services.Profiles.Find("work"));
    }
}
