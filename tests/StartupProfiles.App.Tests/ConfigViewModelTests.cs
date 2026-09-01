using StartupProfiles.App.Config;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

public sealed class ConfigViewModelTests
{
    [Fact]
    public void New_AddsProfileAndSelectsIt()
    {
        using var services = AppTestServices.Create();
        var prompts = new FakeUserPrompts { TextResult = "My New" };
        var viewModel = new ConfigViewModel(services.Profiles, services.Executor, prompts);

        viewModel.NewCommand.Execute(null);

        Assert.Contains(viewModel.Profiles, p => p.Name == "My New");
        Assert.NotNull(viewModel.Editor);
        Assert.Equal("my-new", viewModel.Editor!.Id);
        Assert.NotNull(services.Profiles.Find("my-new"));
    }

    [Fact]
    public void Save_PersistsNameAndActions()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Executor, new FakeUserPrompts());

        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "work");
        viewModel.Editor!.Name = "Work Edited";
        viewModel.Editor.Actions.Add(new ActionEditor { Type = ActionType.OpenUrl, Target = "https://example.com" });
        viewModel.SaveCommand.Execute(null);

        var saved = services.Profiles.Find("work");
        Assert.Equal("Work Edited", saved!.Name);
        Assert.Equal(ActionType.OpenUrl, Assert.Single(saved.Actions).Type);
        Assert.Equal("Work Edited", viewModel.Profiles.First(p => p.Id == "work").Name);
    }

    [Fact]
    public void Delete_WhenConfirmed_RemovesProfile()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Executor, new FakeUserPrompts { ConfirmResult = true });

        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");
        viewModel.DeleteCommand.Execute(null);

        Assert.Null(services.Profiles.Find("games"));
        Assert.DoesNotContain(viewModel.Profiles, p => p.Id == "games");
        Assert.Null(viewModel.Editor);
    }

    [Fact]
    public void Delete_WhenCancelled_KeepsProfile()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Executor, new FakeUserPrompts { ConfirmResult = false });

        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");
        viewModel.DeleteCommand.Execute(null);

        Assert.NotNull(services.Profiles.Find("games"));
    }

    [Fact]
    public void PickIcon_WhenChosen_SetsEditorIcon()
    {
        using var services = AppTestServices.Create();
        var prompts = new FakeUserPrompts { IconResult = "🎯" };
        var viewModel = new ConfigViewModel(services.Profiles, services.Executor, prompts);
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "dev");

        viewModel.PickIconCommand.Execute(null);

        Assert.Equal("🎯", viewModel.Editor!.Icon);
        Assert.Equal("🎯", viewModel.Editor.IconDisplay);
    }

    [Fact]
    public void PickIcon_WhenCancelled_LeavesIconUnchanged()
    {
        using var services = AppTestServices.Create();
        var prompts = new FakeUserPrompts { IconResult = null };
        var viewModel = new ConfigViewModel(services.Profiles, services.Executor, prompts);
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "dev");

        viewModel.PickIconCommand.Execute(null);

        Assert.Equal("💻", viewModel.Editor!.Icon);
    }

    [Fact]
    public void AddAction_ThenMoveUp_ReordersSelectedAction()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "dev");

        viewModel.AddActionCommand.Execute(null);
        viewModel.AddActionCommand.Execute(null);
        var second = viewModel.Editor!.Actions[1];
        viewModel.SelectedAction = second;
        viewModel.MoveUpCommand.Execute(null);

        Assert.Equal(0, viewModel.Editor.Actions.IndexOf(second));
    }
}
