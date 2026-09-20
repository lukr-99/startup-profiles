using StartupProfiles.App.Integration;
using StartupProfiles.Core.Integration;

namespace StartupProfiles.App.Tests;

public sealed class RegistrationViewModelTests
{
    private static RegistrationRequest Request(string? suggested = null) => new()
    {
        AppId = "com.example.app",
        Name = "Example App",
        Target = @"C:\Apps\example.exe",
        Arguments = "--fast",
        SuggestedProfile = suggested,
    };

    private static List<RegistrationChoice> Choices() =>
    [
        RegistrationChoice.ForBase(2),
        RegistrationChoice.ForProfile("dev", "Dev", "💻", 3),
        RegistrationChoice.ForProfile("games", "Games", "🎮", 0),
        RegistrationChoice.ForProfile("everything", "Everything", "✨", 12),
    ];

    [Fact]
    public void SuggestedProfile_IsPreSelected()
    {
        var vm = new RegistrationViewModel(Request(suggested: "dev"), Choices(), new FakeProfileRegistrar());

        Assert.True(vm.Choices.Single(c => c.Id == "dev").IsSelected);
        Assert.False(vm.Choices.Single(c => c.Id == "games").IsSelected);
    }

    [Fact]
    public void EverythingProfile_IsNeverPreSelected_EvenWhenSuggested()
    {
        var vm = new RegistrationViewModel(Request(suggested: "everything"), Choices(), new FakeProfileRegistrar());

        Assert.False(vm.Choices.Single(c => c.Id == "everything").IsSelected);
    }

    [Fact]
    public void Base_IsOfferedFirst_AndNeverPreSelected_EvenWhenSuggested()
    {
        var vm = new RegistrationViewModel(Request(suggested: "base"), Choices(), new FakeProfileRegistrar());

        Assert.True(vm.Choices[0].IsBase);
        Assert.False(vm.Choices[0].IsSelected);
    }

    [Fact]
    public void AddCommand_IsDisabled_UntilADestinationIsSelected()
    {
        var vm = new RegistrationViewModel(Request(), Choices(), new FakeProfileRegistrar());
        Assert.False(vm.AddCommand.CanExecute(null));

        vm.Choices.First().IsSelected = true;

        Assert.True(vm.AddCommand.CanExecute(null));
    }

    [Fact]
    public async Task Add_AppliesToSelectedProfiles_ThenRequestsClose()
    {
        var registrar = new FakeProfileRegistrar();
        var vm = new RegistrationViewModel(Request(), Choices(), registrar);
        vm.Choices.Single(c => c.Id == "dev").IsSelected = true;
        vm.Choices.Single(c => c.Id == "games").IsSelected = true;

        var closed = false;
        vm.CloseRequested += () => closed = true;

        vm.AddCommand.Execute(null);
        await WaitForAsync(() => closed);

        Assert.Equal(["dev", "games"], registrar.AppliedTargets!.ProfileIds);
        Assert.False(registrar.AppliedTargets.IncludeBase);
        Assert.Equal(@"C:\Apps\example.exe", registrar.AppliedRequest!.Target);
        Assert.True(vm.Added);
        Assert.True(closed);
    }

    [Fact]
    public async Task Add_WithBaseTicked_TargetsTheBase()
    {
        var registrar = new FakeProfileRegistrar();
        var vm = new RegistrationViewModel(Request(), Choices(), registrar);
        vm.Choices.Single(c => c.IsBase).IsSelected = true;

        vm.AddCommand.Execute(null);
        await WaitForAsync(() => registrar.AppliedTargets is not null);

        Assert.True(registrar.AppliedTargets!.IncludeBase);
        Assert.Empty(registrar.AppliedTargets.ProfileIds);
    }

    [Fact]
    public async Task Recognize_NeedsNoSelection_AndKeepsTheAppInTheLibraryOnly()
    {
        var registrar = new FakeProfileRegistrar();
        var vm = new RegistrationViewModel(Request(), Choices(), registrar);
        Assert.True(vm.RecognizeCommand.CanExecute(null));

        var closed = false;
        vm.CloseRequested += () => closed = true;

        vm.RecognizeCommand.Execute(null);
        await WaitForAsync(() => closed);

        Assert.True(registrar.AppliedTargets!.IsLibraryOnly);
        Assert.Equal("example-app", vm.Outcome!.LibraryItemId);
        Assert.True(vm.Added);
    }

    [Fact]
    public async Task Add_WhenRegistrarFails_ShowsStatus_AndStaysOpen()
    {
        var registrar = new FakeProfileRegistrar(new RegistrationException("Could not reach the running instance."));
        var vm = new RegistrationViewModel(Request(), Choices(), registrar);
        vm.Choices.First().IsSelected = true;

        var closed = false;
        vm.CloseRequested += () => closed = true;

        vm.AddCommand.Execute(null);
        await WaitForAsync(() => vm.StatusIsError);

        Assert.False(closed);
        Assert.False(vm.Added);
        Assert.Contains("Could not reach", vm.Status!, StringComparison.Ordinal);
        Assert.True(vm.AddCommand.CanExecute(null)); // re-enabled so the user can retry
        Assert.True(vm.RecognizeCommand.CanExecute(null));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10);
    }
}
