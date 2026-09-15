using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StartupProfiles.App.Ui;

/// <summary>
/// Loads icons from the Windows shell (the same source Explorer and the Startup apps page use) for file paths,
/// shortcuts, <c>shell:AppsFolder</c> packaged apps, and registered extensions. Results, including misses, are
/// cached per source; call from the UI thread.
/// </summary>
public static class ShellIcons
{
    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiLargeIcon = 0x0;
    private const uint ShgfiUseFileAttributes = 0x10;
    private const uint ShgfiPidl = 0x8;
    private const uint FileAttributeNormal = 0x80;

    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The icon for <paramref name="source"/> (see <see cref="ActionIconSource.For"/>), or null.</summary>
    public static ImageSource? Get(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        if (Cache.TryGetValue(source, out var cached)) return cached;

        var icon = source.StartsWith('.') ? FromExtension(source) : FromParsingName(Resolve(source));
        Cache[source] = icon;
        return icon;
    }

    /// <summary>
    /// Expands environment variables, finds bare executable names ("explorer.exe") on PATH, and finds a Squirrel
    /// app's exe in its newest <c>app-&lt;version&gt;</c> folder.
    /// </summary>
    private static string Resolve(string source)
    {
        var expanded = Environment.ExpandEnvironmentVariables(source);
        if (expanded.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return expanded;
        if (Path.IsPathFullyQualified(expanded)) return InSquirrelAppFolder(expanded) ?? expanded;
        if (expanded.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)) return expanded;

        var fileName = Path.HasExtension(expanded) ? expanded : expanded + ".exe";
        var searchDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Prepend(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        foreach (var dir in searchDirs)
        {
            try
            {
                var candidate = Path.Combine(Environment.ExpandEnvironmentVariables(dir), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry; skip it.
            }
        }

        return expanded;
    }

    private static string? InSquirrelAppFolder(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) return null;
        if (Path.GetDirectoryName(path) is not { } directory || !Directory.Exists(directory)) return null;

        try
        {
            return Directory.EnumerateDirectories(directory, "app-*")
                .Select(dir => Path.Combine(dir, Path.GetFileName(path)))
                .Where(File.Exists)
                .OrderByDescending(candidate => Version.TryParse(
                    Path.GetFileName(Path.GetDirectoryName(candidate))?["app-".Length..], out var version) ? version : null)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static BitmapSource? FromParsingName(string name)
    {
        if (SHParseDisplayName(name, IntPtr.Zero, out var pidl, 0, out _) != 0 || pidl == IntPtr.Zero) return null;
        try
        {
            var info = new ShFileInfo();
            return SHGetFileInfo(pidl, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiPidl | ShgfiIcon | ShgfiLargeIcon) == IntPtr.Zero
                ? null
                : ToImage(info.hIcon);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    private static BitmapSource? FromExtension(string extension)
    {
        var info = new ShFileInfo();
        return SHGetFileInfo(extension, FileAttributeNormal, ref info, (uint)Marshal.SizeOf<ShFileInfo>(),
            ShgfiIcon | ShgfiLargeIcon | ShgfiUseFileAttributes) == IntPtr.Zero
            ? null
            : ToImage(info.hIcon);
    }

    private static BitmapSource? ToImage(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero) return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            _ = DestroyIcon(hIcon);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(IntPtr pidl, uint fileAttributes, ref ShFileInfo info, uint infoSize, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref ShFileInfo info, uint infoSize, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
