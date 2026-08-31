namespace StartupProfiles.App;

// Skeleton entry point. The real host will:
//   1. Start a loopback-only ASP.NET Core server (Api/) that serves the read/write model.
//   2. Show the minimal launcher window (Launcher/) hosting wwwroot over that server.
//   3. Install a tray icon (Tray/) for switching / re-running profiles after login.
// See docs/ARCHITECTURE.md.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
    }
}
