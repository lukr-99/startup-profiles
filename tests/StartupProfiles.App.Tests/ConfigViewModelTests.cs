using StartupProfiles.App.Config;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Tests;

public sealed class ConfigViewModelTests
{
    [Fact]
    public void New_AddsProfileAndSelectsIt()
    {
        using var services = AppTestServices.Create();
        var prompts = new FakeUserPrompts { TextResult = "My New" };
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, prompts);

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
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());

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
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts { ConfirmResult = true });

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
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts { ConfirmResult = false });

        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");
        viewModel.DeleteCommand.Execute(null);

        Assert.NotNull(services.Profiles.Find("games"));
    }

    [Fact]
    public void PickIcon_WhenChosen_SetsEditorIcon()
    {
        using var services = AppTestServices.Create();
        var prompts = new FakeUserPrompts { IconResult = "🎯" };
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, prompts);
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
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, prompts);
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "dev");

        viewModel.PickIconCommand.Execute(null);

        Assert.Equal("💻", viewModel.Editor!.Icon);
    }

    [Fact]
    public void Base_IsPinnedFirst_AndOpensTheBaseEditor()
    {
        using var services = AppTestServices.Create();
        services.Base.Save(new StartupBase { Actions = [new ProfileAction { Type = ActionType.LaunchApp, Target = "noise.exe" }] });
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());

        var first = viewModel.Profiles[0];
        Assert.True(first.IsBase);
        Assert.Equal(1, first.ActionCount);
        Assert.Single(viewModel.Profiles, p => p.IsBase);

        viewModel.Selected = first;

        Assert.True(viewModel.Editor!.IsBase);
        Assert.Equal("noise.exe", Assert.Single(viewModel.Editor.Actions).Target);
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
        Assert.False(viewModel.PickIconCommand.CanExecute(null));
    }

    [Fact]
    public void Base_Save_PersistsToTheBaseStore_NotAsAProfile()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());

        viewModel.Selected = viewModel.Profiles.Single(p => p.IsBase);
        viewModel.AddActionCommand.Execute(null);
        viewModel.Editor!.Actions[0].Target = "noise.exe";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("noise.exe", Assert.Single(services.Base.Load().Actions).Target);
        Assert.Equal(1, viewModel.Profiles.Single(p => p.IsBase).ActionCount);
        Assert.Equal(6, services.Profiles.GetAll().Count);
    }

    [Fact]
    public void Base_RemoveThenSave_DeletesTheAction()
    {
        using var services = AppTestServices.Create();
        services.Base.Save(new StartupBase
        {
            Actions = [new ProfileAction { Type = ActionType.LaunchApp, Target = "noise.exe" }, new ProfileAction { Type = ActionType.OpenUrl, Target = "https://a" }],
        });
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.Single(p => p.IsBase);

        viewModel.SelectedAction = viewModel.Editor!.Actions[0];
        Assert.True(viewModel.RemoveActionCommand.CanExecute(null));
        viewModel.RemoveActionCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("https://a", Assert.Single(services.Base.Load().Actions).Target);
        Assert.Equal(1, viewModel.Profiles.Single(p => p.IsBase).ActionCount);
    }

    [Fact]
    public void Save_PersistsIncludeBase()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());

        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");
        Assert.True(viewModel.Editor!.IncludeBase);
        viewModel.Editor.IncludeBase = false;
        viewModel.SaveCommand.Execute(null);

        Assert.False(services.Profiles.Find("games")!.IncludeBase);
    }

    [Fact]
    public void StartupApps_ListsLaunchableWindowsStartupApps_ByName_ExceptItself()
    {
        using var services = AppTestServices.Create();
        services.StartupApps.Entries.AddRange(
        [
            Entry("Steam", "steam.exe"),
            Entry("Discord", "Update.exe", enabled: false),
            Entry("StartupProfiles", "StartupProfiles.exe"),
            new StartupEntry { Source = StartupEntrySource.PackagedTask, Key = "Feed", Name = "Feed", IsEnabled = true },
        ]);

        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());

        Assert.Equal(["Discord", "Steam"], viewModel.StartupApps.Select(a => a.Name));
        Assert.True(viewModel.StartupApps.Single(a => a.Name == "Steam").StartsWithWindows);
        Assert.Equal("Registry · off in Windows", viewModel.StartupApps.Single(a => a.Name == "Discord").Detail);
    }

    [Fact]
    public void AddStartupApp_AddsItsLaunchAction_AtTheDropPosition()
    {
        using var services = AppTestServices.Create();
        services.Profiles.Save(new Profile
        {
            Id = "games",
            Name = "Games",
            Actions = [new ProfileAction { Type = ActionType.OpenUrl, Target = "https://a" }, new ProfileAction { Type = ActionType.OpenUrl, Target = "https://b" }],
        });
        services.StartupApps.Entries.Add(Entry("Steam", @"C:\Steam\steam.exe", arguments: "-silent"));
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");

        viewModel.AddStartupApp(viewModel.StartupApps[0], index: 1);

        var added = viewModel.Editor!.Actions[1];
        Assert.Equal(ActionType.LaunchApp, added.Type);
        Assert.Equal(@"C:\Steam\steam.exe", added.Target);
        Assert.Equal("-silent", added.Arguments);
        Assert.Same(added, viewModel.SelectedAction);

        // Dropping saves right away, in the dropped order.
        var saved = services.Profiles.Find("games")!.Actions;
        Assert.Equal(["https://a", @"C:\Steam\steam.exe", "https://b"], saved.Select(a => a.Target));
        Assert.Contains("saved", viewModel.Status, StringComparison.Ordinal);

        // The app went into the library and the row links to it.
        var item = Assert.Single(services.Library.GetAll());
        Assert.Equal("Steam", item.Name);
        Assert.True(added.IsLinked);
        Assert.Equal(item.Id, added.LibraryItemId);
        Assert.Equal("Steam", added.TargetDisplay);
        Assert.Contains(viewModel.Library.Items, r => r.Id == item.Id);
    }

    [Fact]
    public void AddLibraryRow_LinksTheItem_AndSaveStoresTheLink()
    {
        using var services = AppTestServices.Create();
        services.Library.Add(new LibraryItem { Name = "Discord", Target = @"C:\Discord\Update.exe", Arguments = "--processStart Discord.exe" });
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "chill");

        viewModel.AddLibraryRow(viewModel.Library.Items[0]);
        viewModel.Editor!.Actions[0].DelaySeconds = 5;
        viewModel.SaveCommand.Execute(null);

        var saved = Assert.Single(services.Profiles.Find("chill")!.Actions);
        Assert.Equal("discord", saved.LibraryItemId);
        Assert.Equal(@"C:\Discord\Update.exe", saved.Target);
        Assert.Equal(TimeSpan.FromSeconds(5), saved.Delay);
    }

    [Fact]
    public void EditingALibraryItem_UpdatesTheLinkedRowsInTheOpenEditor()
    {
        using var services = AppTestServices.Create();
        var item = services.Library.Add(new LibraryItem { Name = "Steam", Target = @"C:\Steam\steam.exe" });
        services.Profiles.Save(new Profile { Id = "games", Name = "Games", Actions = [item.ApplyTo(new ProfileAction { Type = ActionType.LaunchApp })] });
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");

        viewModel.Library.Selected = viewModel.Library.Items.Single();
        viewModel.Library.Draft!.Name = "Steam (big picture)";
        viewModel.Library.Draft.Arguments = "-bigpicture";
        viewModel.Library.SaveCommand.Execute(null);

        var row = Assert.Single(viewModel.Editor!.Actions);
        Assert.Equal("Steam (big picture)", row.TargetDisplay);
        Assert.Equal("-bigpicture", row.Arguments);
        Assert.Equal("Used by Games.", viewModel.Library.UsedBy);
        Assert.Equal("-bigpicture", services.Library.Find(item.Id)!.Arguments);
    }

    [Fact]
    public void DeletingALibraryItem_KeepsTheRowsAsStandaloneActions()
    {
        using var services = AppTestServices.Create();
        var item = services.Library.Add(new LibraryItem { Name = "Steam", Target = @"C:\Steam\steam.exe" });
        services.Profiles.Save(new Profile { Id = "games", Name = "Games", Actions = [item.ApplyTo(new ProfileAction { Type = ActionType.LaunchApp })] });
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts { ConfirmResult = true });
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");

        viewModel.Library.Selected = viewModel.Library.Items.Single();
        viewModel.Library.DeleteCommand.Execute(null);

        Assert.Empty(viewModel.Library.Items);
        var row = Assert.Single(viewModel.Editor!.Actions);
        Assert.False(row.IsLinked);
        Assert.Equal(@"C:\Steam\steam.exe", row.TargetDisplay);
        var stored = Assert.Single(services.Profiles.Find("games")!.Actions);
        Assert.Null(stored.LibraryItemId);
        Assert.Equal(@"C:\Steam\steam.exe", stored.Target);
    }

    [Fact]
    public void NewLibraryItem_RequiresATarget_ThenSavesIt()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());

        viewModel.Library.NewCommand.Execute(null);
        viewModel.Library.Draft!.Name = "GitHub";
        viewModel.Library.SaveCommand.Execute(null);
        Assert.Empty(services.Library.GetAll());

        viewModel.Library.Draft.Type = ActionType.OpenUrl;
        viewModel.Library.Draft.Target = "https://github.com";
        viewModel.Library.SaveCommand.Execute(null);

        var item = Assert.Single(services.Library.GetAll());
        Assert.Equal("github", item.Id);
        Assert.Same(viewModel.Library.Selected, viewModel.Library.Items.Single());
    }

    [Fact]
    public void SaveActionToLibrary_StoresAStandaloneRow_AndLinksIt()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "work");
        viewModel.AddActionCommand.Execute(null);
        var row = viewModel.Editor!.Actions[0];
        row.Type = ActionType.OpenUrl;
        row.Target = "https://www.office.com/";

        viewModel.SaveActionToLibrary(row);

        var item = Assert.Single(services.Library.GetAll());
        Assert.Equal("office.com", item.Name);
        Assert.Equal(item.Id, row.LibraryItemId);
    }

    [Fact]
    public void MoveAction_DropsTheRowAboveTheTargetRow()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "work");
        foreach (var target in new[] { "a", "b", "c", "d" })
        {
            viewModel.AddActionCommand.Execute(null);
            viewModel.SelectedAction!.Target = target;
        }
        var actions = viewModel.Editor!.Actions;

        viewModel.MoveAction(actions[0], 3);        // a onto d: b c a d
        Assert.Equal(["b", "c", "a", "d"], actions.Select(a => a.Target));

        viewModel.MoveAction(actions[3], 0);        // d onto b: d b c a
        Assert.Equal(["d", "b", "c", "a"], actions.Select(a => a.Target));

        viewModel.MoveAction(actions[1], null);     // b to the end: d c a b
        Assert.Equal(["d", "c", "a", "b"], actions.Select(a => a.Target));
    }

    [Fact]
    public void AddFiles_PutsEachInTheLibrary_AndLinksThemInOrder()
    {
        using var services = AppTestServices.Create();
        var folder = Directory.CreateTempSubdirectory("sp-drop-").FullName;
        try
        {
            var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
            viewModel.Selected = viewModel.Profiles.First(p => p.Id == "work");

            viewModel.AddFiles([folder, @"C:\Tools\notes.txt", @"C:\Tools\app.exe"]);

            Assert.Equal([ActionType.OpenFolder, ActionType.OpenFile, ActionType.LaunchApp], viewModel.Editor!.Actions.Select(a => a.Type));
            Assert.All(viewModel.Editor.Actions, a => Assert.True(a.IsLinked));
            Assert.Equal(3, services.Library.GetAll().Count);
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void AddStartupApp_WithoutAPosition_Appends_AndNeverAddsTheSameAppTwice()
    {
        using var services = AppTestServices.Create();
        services.StartupApps.Entries.Add(Entry("Steam", @"C:\Steam\steam.exe", arguments: "-silent"));
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.Single(p => p.IsBase);

        viewModel.AddStartupApp(viewModel.StartupApps[0]);
        viewModel.AddStartupApp(viewModel.StartupApps[0]);

        Assert.Single(viewModel.Editor!.Actions);
        Assert.Single(services.Library.GetAll());
        Assert.Contains("already", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void Panel_HidesWhatBaseStarts_WhileEditingAProfileThatIncludesIt()
    {
        using var services = AppTestServices.Create();
        var noise = services.Library.Add(new LibraryItem { Name = "Noise", Target = @"C:\AMD\noise.exe" });
        var steam = services.Library.Add(new LibraryItem { Name = "Steam", Target = @"C:\Steam\steam.exe" });
        services.Base.Save(new StartupBase { Actions = [noise.ApplyTo(new ProfileAction { Type = ActionType.LaunchApp })] });
        services.StartupApps.Entries.Add(Entry("AMD Noise", @"C:\AMD\noise.exe"));
        services.StartupApps.Entries.Add(Entry("Discord", @"C:\Discord\Update.exe"));
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        var refreshes = 0;
        viewModel.PanelFilterChanged += () => refreshes++;
        bool Offered(object item) => viewModel.IsOfferedInPanel(item);
        var noiseRow = viewModel.Library.Items.Single(r => r.Id == noise.Id);
        var steamRow = viewModel.Library.Items.Single(r => r.Id == steam.Id);
        var noiseApp = viewModel.StartupApps.Single(a => a.Name == "AMD Noise");
        var discordApp = viewModel.StartupApps.Single(a => a.Name == "Discord");

        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");
        Assert.False(Offered(noiseRow));
        Assert.False(Offered(noiseApp)); // same target as the base's linked item
        Assert.True(Offered(steamRow));
        Assert.True(Offered(discordApp));

        viewModel.Editor!.IncludeBase = false;
        Assert.True(Offered(noiseRow));

        viewModel.Selected = viewModel.Profiles.Single(p => p.IsBase);
        Assert.True(Offered(noiseRow));
        Assert.True(refreshes >= 3);
    }

    [Fact]
    public void AddingSomethingBaseStarts_ToAProfile_IsRefused()
    {
        using var services = AppTestServices.Create();
        var noise = services.Library.Add(new LibraryItem { Name = "Noise", Target = @"C:\AMD\noise.exe" });
        services.Base.Save(new StartupBase { Actions = [noise.ApplyTo(new ProfileAction { Type = ActionType.LaunchApp })] });
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");

        viewModel.AddLibraryItem(noise);
        viewModel.AddFiles([@"C:\AMD\noise.exe"]);

        Assert.Empty(viewModel.Editor!.Actions);
        Assert.Contains("Base", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void DropOnProfile_AddsToThatProfile_AndSaves_WithoutOpeningIt()
    {
        using var services = AppTestServices.Create();
        var noise = services.Library.Add(new LibraryItem { Name = "Noise", Target = @"C:\AMD\noise.exe" });
        services.Base.Save(new StartupBase { Actions = [noise.ApplyTo(new ProfileAction { Type = ActionType.LaunchApp })] });
        services.StartupApps.Entries.Add(Entry("Steam", @"C:\Steam\steam.exe", arguments: "-silent"));
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "work");
        var games = viewModel.Profiles.First(p => p.Id == "games");

        viewModel.DropOnProfile(games, viewModel.StartupApps[0]);
        viewModel.DropOnProfile(games, viewModel.StartupApps[0]);                               // duplicate
        viewModel.DropOnProfile(games, viewModel.Library.Items.Single(r => r.Id == noise.Id));  // Base starts it

        var saved = Assert.Single(services.Profiles.Find("games")!.Actions);
        Assert.Equal(@"C:\Steam\steam.exe", saved.Target);
        Assert.Equal("steam", saved.LibraryItemId);
        Assert.Equal(1, games.ActionCount);
        Assert.Equal("work", viewModel.Editor!.Id); // the open profile did not change
    }

    [Fact]
    public void DropOnBase_AddsToTheBase_AndThenHidesItFromProfiles()
    {
        using var services = AppTestServices.Create();
        var steam = services.Library.Add(new LibraryItem { Name = "Steam", Target = @"C:\Steam\steam.exe" });
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "games");
        var steamRow = viewModel.Library.Items.Single();
        Assert.True(viewModel.IsOfferedInPanel(steamRow));

        viewModel.DropOnProfile(viewModel.Profiles.Single(p => p.IsBase), steamRow);

        Assert.Equal(steam.Id, Assert.Single(services.Base.Load().Actions).LibraryItemId);
        Assert.False(viewModel.IsOfferedInPanel(steamRow));
    }

    [Fact]
    public void DropOnProfile_ATableRow_CopiesItWithItsRunOptions()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "work");
        viewModel.AddActionCommand.Execute(null);
        viewModel.SelectedAction!.Target = @"C:\Tools\vpn.exe";
        viewModel.SelectedAction.DelaySeconds = 3;

        viewModel.DropOnProfile(viewModel.Profiles.First(p => p.Id == "school"), viewModel.SelectedAction);

        var copied = Assert.Single(services.Profiles.Find("school")!.Actions);
        Assert.Equal(@"C:\Tools\vpn.exe", copied.Target);
        Assert.Equal(TimeSpan.FromSeconds(3), copied.Delay);
    }

    [Fact]
    public void AddStartupApp_WithNothingSelected_DoesNothing()
    {
        using var services = AppTestServices.Create();
        services.StartupApps.Entries.Add(Entry("Steam", "steam.exe"));
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());

        viewModel.AddStartupApp(viewModel.StartupApps[0]);

        Assert.Null(viewModel.Editor);
    }

    private static StartupEntry Entry(string name, string target, bool enabled = true, string? arguments = null) => new()
    {
        Source = StartupEntrySource.UserRunKey,
        Key = name,
        Name = name,
        IsEnabled = enabled,
        CanToggle = true,
        Launch = new ProfileAction { Type = ActionType.LaunchApp, Target = target, Arguments = arguments },
    };

    [Fact]
    public void AddAction_ThenMoveUp_ReordersSelectedAction()
    {
        using var services = AppTestServices.Create();
        var viewModel = new ConfigViewModel(services.Profiles, services.Base, services.Library, services.StartupApps, services.Executor, new FakeUserPrompts());
        viewModel.Selected = viewModel.Profiles.First(p => p.Id == "dev");

        viewModel.AddActionCommand.Execute(null);
        viewModel.AddActionCommand.Execute(null);
        var second = viewModel.Editor!.Actions[1];
        viewModel.SelectedAction = second;
        viewModel.MoveUpCommand.Execute(null);

        Assert.Equal(0, viewModel.Editor.Actions.IndexOf(second));
    }
}
