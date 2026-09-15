using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests;

public sealed class StorageTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"sp-{Guid.NewGuid():N}.json");

    [Fact]
    public void ProfileStore_SaveReload_RoundTripsProfile()
    {
        var file = TempFile();
        try
        {
            var profile = new Profile
            {
                Id = "work",
                Name = "Work",
                Icon = "work",
                StartupBehaviour = StartupBehaviour.RememberLast,
                Actions =
                [
                    new ProfileAction
                    {
                        Type = ActionType.OpenUrl,
                        Target = "https://portal.example.com",
                        Delay = TimeSpan.FromSeconds(3),
                        FailureBehaviour = FailureBehaviour.Retry,
                        RetryCount = 2,
                        RunAsAdmin = true,
                    },
                ],
                Conditions = [new ProfileCondition { Type = ConditionType.Network, Value = "corp" }],
            };

            new ProfileStore(file).Save(profile);
            var reloaded = new ProfileStore(file).Find("work");

            Assert.NotNull(reloaded);
            Assert.Equal("Work", reloaded!.Name);
            Assert.Equal(StartupBehaviour.RememberLast, reloaded.StartupBehaviour);
            var action = Assert.Single(reloaded.Actions);
            Assert.Equal(ActionType.OpenUrl, action.Type);
            Assert.Equal(TimeSpan.FromSeconds(3), action.Delay);
            Assert.Equal(FailureBehaviour.Retry, action.FailureBehaviour);
            Assert.Equal(2, action.RetryCount);
            Assert.True(action.RunAsAdmin);
            Assert.Equal(ConditionType.Network, Assert.Single(reloaded.Conditions).Type);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ProfileStore_SerializesEnumsAsNames()
    {
        var file = TempFile();
        try
        {
            new ProfileStore(file).Save(new Profile
            {
                Id = "dev",
                Name = "Dev",
                Actions = [new ProfileAction { Type = ActionType.LaunchApp, Target = "code.exe" }],
            });

            var json = File.ReadAllText(file);
            Assert.Contains("\"LaunchApp\"", json);
            Assert.DoesNotContain("\"Type\": 0", json);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ProfileStore_MissingFile_SeedsDefaultProfiles()
    {
        var store = new ProfileStore(TempFile());

        var names = store.GetAll().Select(p => p.Name).ToArray();

        Assert.Equal(["Work", "Dev", "School", "Games", "Chill", "Everything"], names);
    }

    [Fact]
    public void ProfileStore_Remove_DeletesProfile()
    {
        var file = TempFile();
        try
        {
            var store = new ProfileStore(file);
            store.Remove("work");
            Assert.Null(new ProfileStore(file).Find("work"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ConfigStore_SetReload_RoundTripsValue()
    {
        var file = TempFile();
        try
        {
            new ConfigStore(file).SetValue("lastProfile", "dev");
            Assert.Equal("dev", new ConfigStore(file).GetValue("lastProfile"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void HistoryStore_Append_ReturnsNewestFirst()
    {
        var file = TempFile();
        try
        {
            var store = new HistoryStore(file);
            store.Append(Run("work"));
            store.Append(Run("dev"));

            var recent = new HistoryStore(file).GetRecent();

            Assert.Equal("dev", recent[0].ProfileId);
            Assert.Equal("work", recent[1].ProfileId);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void StartupTakeoverStore_SaveReloadClear_RoundTripsEntries()
    {
        var file = TempFile();
        try
        {
            var entry = new StartupEntry
            {
                Source = StartupEntrySource.PackagedTask,
                Key = @"MSTeams_8wekyb3d8bbwe\TeamsTfwStartupTask",
                Name = "Microsoft Teams",
                IsEnabled = true,
                CanToggle = true,
                Launch = new ProfileAction { Type = ActionType.LaunchApp, Target = "explorer.exe", Arguments = "shell:AppsFolder\\x!y" },
            };

            new StartupTakeoverStore(file).Save([entry]);
            var reloaded = Assert.Single(new StartupTakeoverStore(file).Load());

            Assert.Equal(StartupEntrySource.PackagedTask, reloaded.Source);
            Assert.Equal(entry.Key, reloaded.Key);
            Assert.Equal("explorer.exe", reloaded.Launch!.Target);

            new StartupTakeoverStore(file).Clear();
            Assert.Empty(new StartupTakeoverStore(file).Load());
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static ProfileRun Run(string profileId) => new()
    {
        ProfileId = profileId,
        ProfileName = profileId,
        StartedAt = DateTimeOffset.UnixEpoch,
        Duration = TimeSpan.Zero,
        Actions = [],
    };
}
