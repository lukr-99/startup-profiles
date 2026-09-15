using StartupProfiles.Core.Models;

namespace StartupProfiles.Core.Startup;

/// <summary>Turns a startup command line (a <c>Run</c> key value) into a launch action.</summary>
public static class StartupCommandLine
{
    /// <summary>Splits <paramref name="commandLine"/> into target and arguments, or returns null if it is blank.</summary>
    public static ProfileAction? ToLaunchAction(string? commandLine)
    {
        var text = commandLine?.Trim();
        if (string.IsNullOrEmpty(text)) return null;

        var (target, arguments) = Split(text);
        return target.Length == 0
            ? null
            : new ProfileAction { Type = ActionType.LaunchApp, Target = target, Arguments = arguments };
    }

    private static (string Target, string? Arguments) Split(string text)
    {
        if (text[0] == '"')
        {
            var close = text.IndexOf('"', 1);
            return close < 0 ? (text[1..], null) : (text[1..close], Rest(text[(close + 1)..]));
        }

        // Unquoted paths may contain spaces ("C:\Program Files\App\App.exe /autorun"). Windows resolves them
        // by trying each space-separated prefix; in practice the executable is the first "*.exe" token end.
        var exeEnd = FindExeEnd(text);
        if (exeEnd > 0) return (text[..exeEnd], Rest(text[exeEnd..]));

        var space = text.IndexOfAny([' ', '\t']);
        return space < 0 ? (text, null) : (text[..space], Rest(text[space..]));
    }

    private static int FindExeEnd(string text)
    {
        for (var i = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
             i >= 0;
             i = text.IndexOf(".exe", i + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = i + ".exe".Length;
            if (end == text.Length || char.IsWhiteSpace(text[end])) return end;
        }

        return -1;
    }

    private static string? Rest(string remainder)
    {
        var trimmed = remainder.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
