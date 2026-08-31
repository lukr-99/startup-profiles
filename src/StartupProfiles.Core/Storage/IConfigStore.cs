namespace StartupProfiles.Core.Storage;

/// <summary>Persistence for small key/value app configuration (config.json).</summary>
public interface IConfigStore
{
    string? GetValue(string key);
    string GetValueOrDefault(string key, string fallback);
    void SetValue(string key, string? value);
    IReadOnlyDictionary<string, string> GetAll();
}
