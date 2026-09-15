using System.IO;
using StartupProfiles.Core.Models;

namespace StartupProfiles.App.Config;

/// <summary>Names and actions for things dropped into the editor: table rows and files from Explorer.</summary>
public static class Startables
{
    private const string AppsFolderPrefix = @"shell:AppsFolder\";

    /// <summary>A readable library name for what <paramref name="action"/> starts ("steam", "github.com", "Code").</summary>
    public static string NameFor(ProfileAction action)
    {
        var target = action.Target.Trim().Trim('"');
        var arguments = action.Arguments?.Trim() ?? "";

        if (arguments.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // shell:AppsFolder\Publisher.App_hash!AppId -> App
            var family = arguments[AppsFolderPrefix.Length..].Split('!')[0].Split('_')[0];
            return family[(family.LastIndexOf('.') + 1)..];
        }

        if (action.Type == ActionType.OpenUrl && Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Host.Length > 0)
            return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;

        if (action.Type is ActionType.LaunchApp or ActionType.OpenFile or ActionType.OpenFolder && target.Length > 0)
        {
            var name = Path.GetFileNameWithoutExtension(target.TrimEnd('\\', '/'));
            if (name.Length > 0) return name;
        }

        return target.Length > 0 ? target : action.Type.ToString();
    }

    /// <summary>The action that starts a file or folder dropped from Explorer.</summary>
    public static ProfileAction ActionForPath(string path) => new()
    {
        Type = Directory.Exists(path) ? ActionType.OpenFolder
            : string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) ? ActionType.LaunchApp
            : ActionType.OpenFile,
        Target = path,
    };
}
