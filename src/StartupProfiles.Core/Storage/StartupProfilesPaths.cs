namespace StartupProfiles.Core.Storage;

/// <summary>Resolves on-disk locations for Startup Profiles' local data under %APPDATA%\StartupProfiles.</summary>
public static class StartupProfilesPaths
{
    /// <summary>%APPDATA%\StartupProfiles (created on first access).</summary>
    public static string DataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StartupProfiles");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string ProfilesFile => Path.Combine(DataDirectory, "profiles.json");
    public static string ConfigFile => Path.Combine(DataDirectory, "config.json");
    public static string HistoryFile => Path.Combine(DataDirectory, "history.json");

    /// <summary>Discovery file the running host writes so agents and the tray can find its URL.</summary>
    public static string EndpointFile => Path.Combine(DataDirectory, "endpoint.json");
}
