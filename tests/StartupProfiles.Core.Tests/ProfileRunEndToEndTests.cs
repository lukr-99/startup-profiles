using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.Core.Tests;

/// <summary>
/// End-to-end demonstration with the real <see cref="SystemProcessLauncher"/>: build a profile, run it
/// through <see cref="ProfileRunner"/>, and persist then reload its history. Windows-only because it
/// launches <c>cmd.exe</c>.
/// </summary>
public sealed class ProfileRunEndToEndTests
{
    [Fact]
    public async Task RealLauncher_RunsTrivialProfile_AndPersistsHistory()
    {
        if (!OperatingSystem.IsWindows()) return;

        var registry = ActionHandlerRegistry.CreateDefault(new SystemProcessLauncher());
        var runner = new ProfileRunner(registry, new TaskDelayer());

        var profile = new Profile
        {
            Id = "smoke",
            Name = "Smoke",
            Actions =
            [
                new ProfileAction { Type = ActionType.RunScript, Target = "exit 0" },
                new ProfileAction { Type = ActionType.Delay, Delay = TimeSpan.Zero },
            ],
        };

        var run = await runner.RunAsync(profile);

        Assert.True(run.Succeeded);
        Assert.Equal(2, run.Actions.Count);

        var file = Path.Combine(Path.GetTempPath(), $"sp-hist-{Guid.NewGuid():N}.json");
        try
        {
            new HistoryStore(file).Append(run);
            var reloaded = new HistoryStore(file).GetRecent();

            var stored = Assert.Single(reloaded);
            Assert.Equal("smoke", stored.ProfileId);
            Assert.Equal(2, stored.Actions.Count);
            Assert.True(stored.Succeeded);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
