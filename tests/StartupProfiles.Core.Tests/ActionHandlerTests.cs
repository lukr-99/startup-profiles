using StartupProfiles.Core.Actions;
using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Tests;

public sealed class ActionHandlerTests
{
    private readonly FakeProcessLauncher _launcher = new();

    [Fact]
    public async Task LaunchApp_BuildsSpecWithArguments()
    {
        var handler = new LaunchAppHandler(_launcher);

        var result = await handler.ExecuteAsync(new ProfileAction
        {
            Type = ActionType.LaunchApp,
            Target = "code.exe",
            Arguments = "C:\\repo",
        });

        Assert.True(result.Success);
        var spec = Assert.Single(_launcher.Started);
        Assert.Equal("code.exe", spec.FileName);
        Assert.Equal("C:\\repo", spec.Arguments);
        Assert.False(spec.UseShellExecute);
        Assert.Null(spec.Verb);
    }

    [Fact]
    public async Task LaunchApp_RunAsAdmin_UsesRunasVerb()
    {
        var handler = new LaunchAppHandler(_launcher);

        await handler.ExecuteAsync(new ProfileAction
        {
            Type = ActionType.LaunchApp,
            Target = "tool.exe",
            RunAsAdmin = true,
        });

        var spec = Assert.Single(_launcher.Started);
        Assert.True(spec.UseShellExecute);
        Assert.Equal("runas", spec.Verb);
    }

    [Fact]
    public async Task ShellOpen_UsesShellExecute()
    {
        var handler = new ShellOpenHandler(ActionType.OpenUrl, _launcher);

        await handler.ExecuteAsync(new ProfileAction { Type = ActionType.OpenUrl, Target = "https://example.com" });

        var spec = Assert.Single(_launcher.Started);
        Assert.Equal("https://example.com", spec.FileName);
        Assert.True(spec.UseShellExecute);
    }

    [Fact]
    public async Task RunScript_WrapsCommandInCmd()
    {
        var handler = new RunScriptHandler(_launcher);

        await handler.ExecuteAsync(new ProfileAction
        {
            Type = ActionType.RunScript,
            Target = "setup.bat",
            Arguments = "--quiet",
        });

        var spec = Assert.Single(_launcher.Started);
        Assert.Equal("cmd.exe", spec.FileName);
        Assert.Equal("/c setup.bat --quiet", spec.Arguments);
        Assert.True(spec.CreateNoWindow);
    }

    [Fact]
    public async Task KillProcess_DelegatesToLauncher()
    {
        var handler = new KillProcessHandler(_launcher);

        await handler.ExecuteAsync(new ProfileAction { Type = ActionType.KillProcess, Target = "Teams" });

        Assert.Equal("Teams", Assert.Single(_launcher.Killed));
    }
}
