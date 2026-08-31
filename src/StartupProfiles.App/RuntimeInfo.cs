using System.Text.Json;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App;

/// <summary>
/// Publishes the discovery file (endpoint.json) so agents and the tray can find the running instance's
/// URL without guessing the port. Best-effort: failures never take down the host.
/// </summary>
internal static class RuntimeInfo
{
    public static void Write(int port)
    {
        var info = new
        {
            url = $"http://127.0.0.1:{port}",
            port,
            pid = Environment.ProcessId,
            version = typeof(RuntimeInfo).Assembly.GetName().Version?.ToString(),
            startedAt = DateTimeOffset.UtcNow,
        };
        try { File.WriteAllText(StartupProfilesPaths.EndpointFile, JsonSerializer.Serialize(info)); }
        catch (IOException) { /* best effort */ }
    }

    public static string? ReadUrl()
    {
        try
        {
            if (!File.Exists(StartupProfilesPaths.EndpointFile)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(StartupProfilesPaths.EndpointFile));
            return doc.RootElement.GetProperty("url").GetString();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public static void Delete()
    {
        try { if (File.Exists(StartupProfilesPaths.EndpointFile)) File.Delete(StartupProfilesPaths.EndpointFile); }
        catch (IOException) { /* ignore */ }
    }
}
