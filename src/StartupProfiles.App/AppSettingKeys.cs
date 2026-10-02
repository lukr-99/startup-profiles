namespace StartupProfiles.App;

/// <summary>Keys of the app's own settings in config.json.</summary>
public static class AppSettingKeys
{
    /// <summary>"false" turns off the check for new startup apps when the app starts; anything else leaves it on.</summary>
    public const string DiscoverStartupApps = "discoverStartupApps";

    /// <summary>"false" turns off the daily check for a new version; anything else leaves it on.</summary>
    public const string CheckForUpdates = "checkForUpdates";

    /// <summary>When the last update check ran (round-trip "O" format), so the automatic check runs once a day.</summary>
    public const string LastUpdateCheck = "lastUpdateCheck";
}
