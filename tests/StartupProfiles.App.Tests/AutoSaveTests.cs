using StartupProfiles.App.Config;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Tests;

public sealed class AutoSaveTests
{
    private static ConfigViewModel Open(AppTestServices services, string profileId, ISaveScheduler? saves = null)
    {
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps,
            services.Executor, new FakeUserPrompts(), saves);
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == profileId);
        return viewModel;
    }

    [Fact]
    public void EditingTheName_SavesWithoutPressingSave()
    {
        using var services = AppTestServices.Create();
        var viewModel = Open(services, "work");

        viewModel.Editor!.Name = "Office";

        Assert.Equal("Office", services.Profiles.Find("work")!.Name);
        Assert.Equal("Office", viewModel.Profiles.First(p => p.Id == "work").Name);
        Assert.Equal("All changes saved", viewModel.SaveState);
    }

    [Fact]
    public void EditingARow_SavesIt()
    {
        using var services = AppTestServices.Create();
        var viewModel = Open(services, "work");
        viewModel.AddActionCommand.Execute(null);

        viewModel.SelectedAction!.Type = ActionType.OpenUrl;
        viewModel.SelectedAction.Target = "https://example.com";
        viewModel.SelectedAction.DelaySeconds = 4;

        var saved = Assert.Single(services.Profiles.Find("work")!.Actions);
        Assert.Equal(ActionType.OpenUrl, saved.Type);
        Assert.Equal("https://example.com", saved.Target);
        Assert.Equal(TimeSpan.FromSeconds(4), saved.Delay);
    }

    [Fact]
    public void ChangingTheIcon_SavesIt_AndUpdatesTheSidebar()
    {
        using var services = AppTestServices.Create();
        var viewModel = Open(services, "dev");

        viewModel.Editor!.Icon = "🧪";

        Assert.Equal("🧪", services.Profiles.Find("dev")!.Icon);
        Assert.Equal("🧪", viewModel.Profiles.First(p => p.Id == "dev").Glyph);
    }

    [Fact]
    public void ABlankName_IsNotSaved_AndSaysWhy()
    {
        using var services = AppTestServices.Create();
        var viewModel = Open(services, "work");
        var before = services.Profiles.Find("work")!.Name;

        viewModel.Editor!.Name = "  ";

        Assert.Equal(before, services.Profiles.Find("work")!.Name);
        Assert.Contains("name", viewModel.SaveState, StringComparison.Ordinal);
    }

    [Fact]
    public void WaitingEdits_AreWritten_WhenAnotherProfileIsOpened()
    {
        using var services = AppTestServices.Create();
        var saves = new ManualSaveScheduler();
        var viewModel = Open(services, "work", saves);

        viewModel.Editor!.Name = "Office";
        Assert.True(saves.HasPending);
        Assert.NotEqual("Office", services.Profiles.Find("work")!.Name);

        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");

        Assert.Equal("Office", services.Profiles.Find("work")!.Name);
        Assert.False(saves.HasPending);
    }

    [Fact]
    public void WaitingEdits_AreWritten_WhenTheWindowFlushes()
    {
        using var services = AppTestServices.Create();
        var saves = new ManualSaveScheduler();
        var viewModel = Open(services, "work", saves);

        viewModel.Editor!.IncludeBase = false;
        viewModel.FlushPendingSave();

        Assert.False(services.Profiles.Find("work")!.IncludeBase);
    }

    [Fact]
    public void DeletingAProfile_DropsItsWaitingSave()
    {
        using var services = AppTestServices.Create();
        var saves = new ManualSaveScheduler();
        var viewModel = Open(services, "games", saves);

        viewModel.Editor!.Name = "Gaming";
        viewModel.DeleteCommand.Execute(null);
        saves.Flush();

        Assert.Null(services.Profiles.Find("games"));
    }

    [Fact]
    public void RemovingARow_SavesAtOnce_AndUndoPutsItBackInPlace()
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(new Profile
        {
            Id = "work",
            Name = "Work",
            Actions =
            [
                new ProfileAction { Type = ActionType.OpenUrl, Target = "https://a.example" },
                new ProfileAction { Type = ActionType.OpenUrl, Target = "https://b.example" },
                new ProfileAction { Type = ActionType.OpenUrl, Target = "https://c.example" },
            ],
        });
        var viewModel = Open(services, "work");
        var middle = viewModel.Editor!.Actions[1];

        Assert.False(viewModel.CanUndoRemove);
        viewModel.RemoveActionCommand.Execute(middle);

        Assert.Equal(2, services.Profiles.Find("work")!.Actions.Count);
        Assert.True(viewModel.CanUndoRemove);
        Assert.Contains("b.example", viewModel.Status, StringComparison.Ordinal);

        viewModel.UndoRemoveCommand.Execute(null);

        var targets = services.Profiles.Find("work")!.Actions.Select(a => a.Target).ToList();
        Assert.Equal(["https://a.example", "https://b.example", "https://c.example"], targets);
        Assert.Same(middle, viewModel.SelectedAction);
        Assert.False(viewModel.CanUndoRemove);
    }

    [Fact]
    public void Undo_IsOnlyOffered_ForTheProfileTheRowCameFrom()
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(new Profile
        {
            Id = "work",
            Name = "Work",
            Actions = [new ProfileAction { Type = ActionType.OpenUrl, Target = "https://a.example" }],
        });
        var viewModel = Open(services, "work");

        viewModel.RemoveActionCommand.Execute(viewModel.Editor!.Actions[0]);
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");

        Assert.False(viewModel.CanUndoRemove);
        Assert.False(viewModel.UndoRemoveCommand.CanExecute(null));
    }

    [Fact]
    public void Browse_FillsTheTarget_ForAnAppRow()
    {
        using var services = AppTestServices.Create();
        var prompts = new FakeUserPrompts { TargetResult = @"C:\Tools\app.exe" };
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps,
            services.Executor, prompts);
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "work");
        viewModel.AddActionCommand.Execute(null);

        viewModel.BrowseTargetCommand.Execute(viewModel.SelectedAction);

        Assert.Equal(@"C:\Tools\app.exe", viewModel.SelectedAction!.Target);
        Assert.Equal(@"C:\Tools\app.exe", Assert.Single(services.Profiles.Find("work")!.Actions).Target);
    }
}
