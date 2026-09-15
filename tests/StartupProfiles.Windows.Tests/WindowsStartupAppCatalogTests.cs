using Microsoft.Win32;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Startup;
using StartupProfiles.Windows;

namespace StartupProfiles.Windows.Tests;

/// <summary>
/// Platform test for <see cref="WindowsStartupAppCatalog"/> against throwaway HKCU subkeys standing in for
/// the user and machine hives, and temp folders standing in for the Startup folders. Never touches the real
/// startup registrations. Windows-only.
/// </summary>
public sealed class WindowsStartupAppCatalogTests : IDisposable
{
    private const string TestRoot = @"Software\StartupProfilesCatalogTests";
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";
    private const string AppModelPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel";

    private readonly string _rootPath = $@"{TestRoot}\{Guid.NewGuid():N}";
    private readonly string _tempDir = Directory.CreateTempSubdirectory("sp-catalog-").FullName;

    private string UserPath => $@"{_rootPath}\user";
    private string MachinePath => $@"{_rootPath}\machine";
    private string UserStartupFolder => Path.Combine(_tempDir, "Startup");
    private string CommonStartupFolder => Path.Combine(_tempDir, "CommonStartup");

    [Fact]
    public void GetEntries_ReadsUserRunKey_WithApprovedState()
    {
        if (!OperatingSystem.IsWindows()) return;

        SetValue(UserPath, RunPath, "Steam", "\"C:\\Steam\\steam.exe\" -silent");
        SetValue(UserPath, RunPath, "Spotify", @"C:\Spotify\Spotify.exe --minimized");
        SetValue(UserPath, RunPath, "Docker", @"C:\Docker\Docker Desktop.exe");
        SetValue(UserPath, RunPath, "Security", @"C:\Security.exe");
        SetBinary(UserPath, $@"{ApprovedPath}\Run", "Spotify", Flag(0x03));
        SetBinary(UserPath, $@"{ApprovedPath}\Run", "Docker", Flag(0x01));
        SetBinary(UserPath, $@"{ApprovedPath}\Run", "Security", Flag(0x06));

        var entries = WithCatalog(c => c.GetEntries());

        Assert.True(Find(entries, "Steam").IsEnabled);
        Assert.False(Find(entries, "Spotify").IsEnabled);
        Assert.False(Find(entries, "Docker").IsEnabled);
        Assert.True(Find(entries, "Security").IsEnabled);

        var steam = Find(entries, "Steam");
        Assert.Equal(StartupEntrySource.UserRunKey, steam.Source);
        Assert.True(steam.CanToggle);
        Assert.Equal(@"C:\Steam\steam.exe", steam.Launch!.Target);
        Assert.Equal("-silent", steam.Launch.Arguments);
    }

    [Fact]
    public void GetEntries_NamesRunEntriesByTheExeDescription_WhenItHasOne()
    {
        if (!OperatingSystem.IsWindows()) return;

        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        SetValue(UserPath, RunPath, "cmd-autostart_1234", $"\"{cmd}\" /k");
        SetValue(UserPath, RunPath, "Missing", @"C:\Nope\missing.exe");

        var entries = WithCatalog(c => c.GetEntries());

        Assert.Equal(System.Diagnostics.FileVersionInfo.GetVersionInfo(cmd).FileDescription, Find(entries, "cmd-autostart_1234").Name);
        Assert.Equal("Missing", Find(entries, "Missing").Name);
    }

    [Fact]
    public void GetEntries_MarksAllUsersEntries_AsNotToggleable()
    {
        if (!OperatingSystem.IsWindows()) return;

        SetValue(MachinePath, RunPath, "SecurityHealth", @"C:\Windows\SecurityHealthSystray.exe");
        SetValue(MachinePath, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Legacy", @"C:\Legacy.exe");
        SetBinary(MachinePath, $@"{ApprovedPath}\Run32", "Legacy", Flag(0x03));

        var entries = WithCatalog(c => c.GetEntries());

        var health = Find(entries, "SecurityHealth");
        Assert.Equal(StartupEntrySource.MachineRunKey, health.Source);
        Assert.True(health.IsEnabled);
        Assert.False(health.CanToggle);

        var legacy = Find(entries, "Legacy");
        Assert.Equal(StartupEntrySource.MachineRunKey32, legacy.Source);
        Assert.False(legacy.IsEnabled);
        Assert.False(legacy.CanToggle);
    }

    [Fact]
    public void SetEnabled_UserRunKey_WritesTheApprovedFlag_AndBack()
    {
        if (!OperatingSystem.IsWindows()) return;

        SetValue(UserPath, RunPath, "Steam", @"C:\Steam\steam.exe");

        WithCatalog(catalog =>
        {
            var steam = Find(catalog.GetEntries(), "Steam");

            catalog.SetEnabled(steam, enabled: false);
            Assert.False(Find(catalog.GetEntries(), "Steam").IsEnabled);
            Assert.Equal(0x03, ReadBinary(UserPath, $@"{ApprovedPath}\Run", "Steam")![0]);

            catalog.SetEnabled(steam, enabled: true);
            Assert.True(Find(catalog.GetEntries(), "Steam").IsEnabled);
            Assert.Equal(0x02, ReadBinary(UserPath, $@"{ApprovedPath}\Run", "Steam")![0]);

            // The registration itself is left in place.
            Assert.NotNull(ReadValue(UserPath, RunPath, "Steam"));
        });
    }

    [Fact]
    public void SetEnabled_IsANoOp_WhenTheEntryIsGone()
    {
        if (!OperatingSystem.IsWindows()) return;

        var gone = new StartupEntry { Source = StartupEntrySource.UserRunKey, Key = "Gone", Name = "Gone" };

        WithCatalog(c => c.SetEnabled(gone, enabled: true));

        Assert.Null(ReadBinary(UserPath, $@"{ApprovedPath}\Run", "Gone"));
    }

    [Fact]
    public void SetEnabled_AllUsersEntry_Throws()
    {
        if (!OperatingSystem.IsWindows()) return;

        var machine = new StartupEntry { Source = StartupEntrySource.MachineRunKey, Key = "X", Name = "X" };

        WithCatalog(c => Assert.Throws<InvalidOperationException>(() => c.SetEnabled(machine, enabled: false)));
    }

    [Fact]
    public void StartupFolder_ListsShortcuts_OpensThemViaTheShell_AndToggles()
    {
        if (!OperatingSystem.IsWindows()) return;

        Directory.CreateDirectory(UserStartupFolder);
        File.WriteAllText(Path.Combine(UserStartupFolder, "Relay.lnk"), "");
        File.WriteAllText(Path.Combine(UserStartupFolder, "desktop.ini"), "");

        WithCatalog(catalog =>
        {
            var relay = Assert.Single(catalog.GetEntries());
            Assert.Equal(StartupEntrySource.UserStartupFolder, relay.Source);
            Assert.Equal("Relay", relay.Name);
            Assert.Equal("Relay.lnk", relay.Key);
            Assert.True(relay.IsEnabled);
            Assert.Equal(ActionType.OpenFile, relay.Launch!.Type);
            Assert.Equal(Path.Combine(UserStartupFolder, "Relay.lnk"), relay.Launch.Target);

            catalog.SetEnabled(relay, enabled: false);

            Assert.False(Assert.Single(catalog.GetEntries()).IsEnabled);
            Assert.Equal(0x03, ReadBinary(UserPath, $@"{ApprovedPath}\StartupFolder", "Relay.lnk")![0]);
        });
    }

    [Fact]
    public void PackagedTask_ResolvesTheAppFromItsManifest_AndToggles()
    {
        if (!OperatingSystem.IsWindows()) return;

        const string family = "OpenAI.ChatGPT-Desktop_2p2nqsd0c76g0";
        var packageRoot = Path.Combine(_tempDir, "ChatGPT");
        Directory.CreateDirectory(packageRoot);
        File.WriteAllText(Path.Combine(packageRoot, "AppxManifest.xml"), """
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5">
              <Applications>
                <Application Id="ChatGPT" Executable="app\ChatGPT.exe" EntryPoint="Windows.FullTrustApplication">
                  <Extensions>
                    <uap5:Extension Category="windows.startupTask" Executable="app\ChatGPT.exe" EntryPoint="Windows.FullTrustApplication">
                      <uap5:StartupTask TaskId="ChatGPT" Enabled="true" DisplayName="ChatGPT Classic" />
                    </uap5:Extension>
                  </Extensions>
                </Application>
              </Applications>
            </Package>
            """);
        SetValue(UserPath, $@"{AppModelPath}\Repository\Packages\OpenAI.ChatGPT-Desktop_1.2026.190.0_x64__2p2nqsd0c76g0",
            "PackageRootFolder", packageRoot);
        SetDword(UserPath, $@"{AppModelPath}\SystemAppData\{family}\ChatGPT", "State", 2);
        SetDword(UserPath, $@"{AppModelPath}\SystemAppData\Unrelated_abc\SomethingElse", "Other", 1);

        WithCatalog(catalog =>
        {
            var task = Assert.Single(catalog.GetEntries());
            Assert.Equal(StartupEntrySource.PackagedTask, task.Source);
            Assert.Equal($@"{family}\ChatGPT", task.Key);
            Assert.Equal("ChatGPT Classic", task.Name);
            Assert.True(task.IsEnabled);
            Assert.True(task.CanToggle);
            Assert.Equal("explorer.exe", task.Launch!.Target);
            Assert.Equal($@"shell:AppsFolder\{family}!ChatGPT", task.Launch.Arguments);

            catalog.SetEnabled(task, enabled: false);
            Assert.Equal(1, ReadValue(UserPath, $@"{AppModelPath}\SystemAppData\{family}\ChatGPT", "State"));
            Assert.False(Assert.Single(catalog.GetEntries()).IsEnabled);

            catalog.SetEnabled(task, enabled: true);
            Assert.Equal(2, ReadValue(UserPath, $@"{AppModelPath}\SystemAppData\{family}\ChatGPT", "State"));
        });
    }

    [Fact]
    public void PackagedTask_WithoutAManifest_IsListedButNotLaunchable()
    {
        if (!OperatingSystem.IsWindows()) return;

        SetDword(UserPath, $@"{AppModelPath}\SystemAppData\Microsoft.YourPhone_8wekyb3d8bbwe\YourPhone.Start", "State", 2);

        var task = Assert.Single(WithCatalog(c => c.GetEntries()));

        Assert.Equal("YourPhone", task.Name);
        Assert.Null(task.Launch);
    }

    [Fact]
    public void PackagedTask_SetByPolicy_IsNotToggleable()
    {
        if (!OperatingSystem.IsWindows()) return;

        SetDword(UserPath, $@"{AppModelPath}\SystemAppData\Contoso.App_abc\Start", "State", 4);

        var task = Assert.Single(WithCatalog(c => c.GetEntries()));

        Assert.True(task.IsEnabled);
        Assert.False(task.CanToggle);
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(TestRoot, throwOnMissingSubKey: false); }
            catch (System.Security.SecurityException) { }
        }

        Directory.Delete(_tempDir, recursive: true);
    }

    private T WithCatalog<T>(Func<WindowsStartupAppCatalog, T> use)
    {
        using var user = Registry.CurrentUser.CreateSubKey(UserPath);
        using var machine = Registry.CurrentUser.CreateSubKey(MachinePath);
        return use(new WindowsStartupAppCatalog(user, machine, UserStartupFolder, CommonStartupFolder, TimeProvider.System));
    }

    private void WithCatalog(Action<WindowsStartupAppCatalog> use) => WithCatalog(c =>
    {
        use(c);
        return 0;
    });

    private static StartupEntry Find(IEnumerable<StartupEntry> entries, string key) => entries.Single(e => e.Key == key);

    private static byte[] Flag(byte state) => [state, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static void SetValue(string root, string path, string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{root}\{path}");
        key.SetValue(name, value, RegistryValueKind.String);
    }

    private static void SetBinary(string root, string path, string name, byte[] value)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{root}\{path}");
        key.SetValue(name, value, RegistryValueKind.Binary);
    }

    private static void SetDword(string root, string path, string name, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{root}\{path}");
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static object? ReadValue(string root, string path, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{root}\{path}");
        return key?.GetValue(name);
    }

    private static byte[]? ReadBinary(string root, string path, string name) => ReadValue(root, path, name) as byte[];
}
