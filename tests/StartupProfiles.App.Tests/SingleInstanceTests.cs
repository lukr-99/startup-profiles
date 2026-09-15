namespace StartupProfiles.App.Tests;

public sealed class SingleInstanceTests
{
    private static string UniqueName() => $"StartupProfiles.Tests.{Guid.NewGuid():N}";

    [Fact]
    public void OnlyTheFirstInstanceOwnsTheName()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);
        using var second = new SingleInstance(name);

        Assert.True(first.IsFirst);
        Assert.False(second.IsFirst);
    }

    [Fact]
    public void LaterLaunch_SignalsTheRunningInstance()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);
        using var shown = new ManualResetEventSlim();
        first.Listen(shown.Set);

        using (var second = new SingleInstance(name)) second.SignalFirst();

        Assert.True(shown.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void SignalSentBeforeListening_IsStillDelivered()
    {
        var name = UniqueName();
        using var first = new SingleInstance(name);
        using (var second = new SingleInstance(name)) second.SignalFirst();

        using var shown = new ManualResetEventSlim();
        first.Listen(shown.Set);

        Assert.True(shown.Wait(TimeSpan.FromSeconds(5)));
    }
}
