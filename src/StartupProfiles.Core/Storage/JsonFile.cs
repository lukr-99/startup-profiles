using System.Text.Json;
using System.Text.Json.Serialization;

namespace StartupProfiles.Core.Storage;

/// <summary>Load/save helper for the JSON files that back the stores. Writes atomically via a temp file.</summary>
internal static class JsonFile
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T Load<T>(string path, Func<T> fallback)
    {
        if (!File.Exists(path)) return fallback();

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) return fallback();

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? fallback();
        }
        catch (JsonException)
        {
            // Corrupt or hand-edited file: fall back rather than crash the app at startup.
            return fallback();
        }
    }

    public static void Save<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
}
