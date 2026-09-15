using System.Xml;
using System.Xml.Linq;

namespace StartupProfiles.Windows;

/// <summary>Finds which application in an AppxManifest.xml owns a given startup task.</summary>
internal static class AppxManifestReader
{
    public static PackagedStartupApp? FindStartupTask(string manifestPath, string taskId)
    {
        XDocument manifest;
        try
        {
            manifest = XDocument.Load(manifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            return null;
        }

        // The StartupTask element lives under uap5:/desktop: extension namespaces that vary by manifest
        // version, so match on local names only.
        var task = manifest.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "StartupTask" && (string?)e.Attribute("TaskId") == taskId);
        var application = task?.Ancestors().FirstOrDefault(e => e.Name.LocalName == "Application");
        if (task is null || application?.Attribute("Id")?.Value is not { Length: > 0 } appId) return null;

        var displayName = task.Attribute("DisplayName")?.Value;
        return new PackagedStartupApp(appId, IsLiteral(displayName) ? displayName : null);
    }

    private static bool IsLiteral(string? name) =>
        !string.IsNullOrWhiteSpace(name) && !name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase);
}
