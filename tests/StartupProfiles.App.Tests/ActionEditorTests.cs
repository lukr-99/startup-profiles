using StartupProfiles.App.Config;
using StartupProfiles.App.Ui;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Tests;

public sealed class ActionEditorTests
{
    [Fact]
    public void Row_ReadsInPlainWords()
    {
        var row = new ActionEditor { Type = ActionType.LaunchApp, Target = @"C:\Apps\Code.exe" };

        Assert.Equal("Code", row.DisplayName);
        Assert.Equal(@"App · C:\Apps\Code.exe", row.Summary);
        Assert.Equal("Program", row.TargetLabel);
        Assert.Empty(row.Badges);
    }

    [Fact]
    public void ANewRow_IsNamedForItsKind()
    {
        Assert.Equal("New website", new ActionEditor { Type = ActionType.OpenUrl }.DisplayName);
    }

    [Fact]
    public void AStoreApp_HidesItsShellCommand()
    {
        var row = new ActionEditor
        {
            Type = ActionType.LaunchApp,
            Target = @"C:\Windows\explorer.exe",
            Arguments = @"shell:AppsFolder\MSTeams_8wekyb3d8bbwe!MSTeams",
        };

        Assert.Equal("App · Microsoft Store app", row.Summary);
    }

    [Fact]
    public void Badges_ShowOnlyWhatDiffersFromTheDefaults()
    {
        var row = new ActionEditor
        {
            Type = ActionType.LaunchApp,
            Target = "x.exe",
            DelaySeconds = 5,
            RunAsAdmin = true,
            FailureBehaviour = FailureBehaviour.Retry,
            RetryCount = 3,
        };

        Assert.Equal(["waits 5 s", "as admin", "retries 3x"], row.Badges);
    }

    [Fact]
    public void Changed_FiresForStoredValues_AndNotForNoOps()
    {
        var row = new ActionEditor { Target = "a.exe" };
        var count = 0;
        row.Changed += _ => count++;

        row.Target = "a.exe";
        row.Target = "b.exe";
        row.DelaySeconds = 2;

        Assert.Equal(2, count);
    }

    [Fact]
    public void NegativeDelaysAndZeroRetries_AreClamped()
    {
        var row = new ActionEditor { DelaySeconds = -4, RetryCount = 0 };

        Assert.Equal(0, row.DelaySeconds);
        Assert.Equal(1, row.RetryCount);
    }

    [Fact]
    public void ALinkedRow_IsNotEditable_AndShowsTheItemName()
    {
        var row = new ActionEditor();
        row.LinkTo(new LibraryItem { Id = "steam", Name = "Steam", Target = @"C:\Steam\steam.exe" });

        Assert.False(row.IsEditable);
        Assert.False(row.CanBrowse);
        Assert.Equal("Steam", row.DisplayName);
    }

    [Fact]
    public void ProfileEditor_RaisesChanged_ForRowEdits_AndListChanges()
    {
        var editor = ProfileEditor.FromProfile(new Profile
        {
            Id = "p",
            Name = "P",
            Actions = [new ProfileAction { Type = ActionType.OpenUrl, Target = "https://a.example" }],
        });
        var count = 0;
        editor.Changed += () => count++;

        editor.Actions[0].Target = "https://b.example";
        editor.Actions.Add(new ActionEditor());
        var added = editor.Actions[1];
        editor.Actions.Remove(added);
        added.Target = "ignored after removal";

        Assert.Equal(3, count);
    }

    [Fact]
    public void SidebarRow_CountsSteps_InWords()
    {
        var item = new ProfileListItem("p", "Play", 0, icon: "🎮");

        Assert.Equal("Nothing yet", item.CountText);
        item.ActionCount = 1;
        Assert.Equal("1 step", item.CountText);
        item.ActionCount = 4;
        Assert.Equal("4 steps", item.CountText);
        Assert.Equal("🎮", item.Glyph);
    }

    [Fact]
    public void IconCatalog_HasNoDuplicates()
    {
        var all = IconCatalog.All.ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void EveryActionType_HasAPlainLabel()
    {
        Assert.All(Enum.GetValues<ActionType>(), t => Assert.NotEqual(t.ToString(), ActionLabels.For(t)));
    }
}
