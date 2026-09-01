using System.IO;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Diagnostics;

/// <summary>Appends unhandled exceptions to error.log in the data directory, so crashes are diagnosable.</summary>
internal static class CrashLog
{
    public static string Path => System.IO.Path.Combine(StartupProfilesPaths.DataDirectory, "error.log");

    public static void Write(string source, Exception exception) => Write(source, exception.ToString());

    public static void Write(string source, string detail)
    {
        try
        {
            File.AppendAllText(Path, $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}{detail}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException) { /* logging must never itself throw */ }
        catch (UnauthorizedAccessException) { /* ditto */ }
    }
}
