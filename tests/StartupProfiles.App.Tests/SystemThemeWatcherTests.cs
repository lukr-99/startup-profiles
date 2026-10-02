using Microsoft.Win32;
using StartupProfiles.App.Themes;

namespace StartupProfiles.App.Tests;

public sealed class SystemThemeWatcherTests : IDisposable
{
    private readonly List<SystemThemeWatcher> _watchers = [];
    private bool _dark;
    private int _raised;

    private SystemThemeWatcher Create()
    {
        var watcher = new SystemThemeWatcher(() => _dark);
        watcher.Changed += (_, _) => _raised++;
        _watchers.Add(watcher);
        return watcher;
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers) watcher.Stop();
    }

    [Fact]
    public void Handle_GeneralChangeThatFlipsTheSetting_RaisesChanged()
    {
        var watcher = Create();
        watcher.Start();

        _dark = true;
        watcher.Handle(UserPreferenceCategory.General);

        Assert.Equal(1, _raised);
    }

    [Fact]
    public void Handle_GeneralChangeThatKeepsTheSetting_DoesNotRaise()
    {
        var watcher = Create();
        watcher.Start();

        watcher.Handle(UserPreferenceCategory.General);

        Assert.Equal(0, _raised);
    }

    [Fact]
    public void Handle_OtherCategory_DoesNotRaise()
    {
        var watcher = Create();
        watcher.Start();

        _dark = true;
        watcher.Handle(UserPreferenceCategory.Color);

        Assert.Equal(0, _raised);
    }

    [Fact]
    public void Handle_AfterStop_DoesNotRaise()
    {
        var watcher = Create();
        watcher.Start();
        watcher.Stop();

        _dark = true;
        watcher.Handle(UserPreferenceCategory.General);

        Assert.Equal(0, _raised);
        Assert.False(watcher.IsListening);
    }

    [Fact]
    public void Start_AfterAChangeWhileStopped_DoesNotReportIt()
    {
        var watcher = Create();
        watcher.Start();
        watcher.Stop();

        _dark = true;
        watcher.Start();
        watcher.Handle(UserPreferenceCategory.General);

        Assert.Equal(0, _raised);
        Assert.True(watcher.IsListening);
    }
}
