namespace StartupProfiles.App;

/// <summary>Keys of the app's own settings in config.json.</summary>
public static class AppSettingKeys
{
    /// <summary>"false" turns off the check for new startup apps when the app starts; anything else leaves it on.</summary>
    public const string DiscoverStartupApps = "discoverStartupApps";
}
