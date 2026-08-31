using System.Globalization;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App;

/// <summary>Parsed command-line options for the host.</summary>
internal sealed record LaunchOptions(int Port, bool Headless, string[] RawArgs)
{
    private const int FallbackPort = 8790;

    public static LaunchOptions Parse(string[] args)
    {
        var port = ResolveDefaultPort();
        var headless = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--port" when i + 1 < args.Length && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var p):
                    port = p;
                    i++;
                    break;
                case "--headless" or "--no-tray" or "--server":
                    headless = true;
                    break;
            }
        }
        return new LaunchOptions(port, headless, args);
    }

    private static int ResolveDefaultPort()
    {
        var configured = new ConfigStore().GetValueOrDefault("port", FallbackPort.ToString(CultureInfo.InvariantCulture));
        return int.TryParse(configured, CultureInfo.InvariantCulture, out var p) ? p : FallbackPort;
    }
}
