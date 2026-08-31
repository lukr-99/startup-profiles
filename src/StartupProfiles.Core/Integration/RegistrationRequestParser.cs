using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace StartupProfiles.Core.Integration;

/// <summary>
/// Parses the two public transports of the integration contract into a <see cref="RegistrationRequest"/>:
/// the <c>startupprofiles://register?...</c> protocol URI and the <c>register --app-id ...</c> CLI form
/// (see docs/INTEGRATION.md). Both funnel through the same field mapping and validation, so the two
/// transports can never drift apart.
/// </summary>
public static class RegistrationRequestParser
{
    /// <summary>The scheme of the registration protocol URI.</summary>
    public const string Scheme = "startupprofiles";

    /// <summary>The action segment expected after the scheme (<c>startupprofiles://register</c>).</summary>
    public const string RegisterAction = "register";

    /// <summary>Parses a <c>startupprofiles://register?appId=...&amp;name=...&amp;target=...</c> URI.</summary>
    public static bool TryParseUri(string? uri, [NotNullWhen(true)] out RegistrationRequest? request, out string error)
    {
        request = null;
        if (string.IsNullOrWhiteSpace(uri))
        {
            error = "No registration URI was supplied.";
            return false;
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Not a {Scheme}:// registration URI.";
            return false;
        }

        // The action is the authority (startupprofiles://register?...) or the first path segment
        // (startupprofiles:register?...), depending on how the caller formed the URI.
        var action = string.IsNullOrEmpty(parsed.Host) ? parsed.AbsolutePath.Trim('/') : parsed.Host;
        if (!string.Equals(action, RegisterAction, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Unsupported action '{action}'. Only '{RegisterAction}' is supported.";
            return false;
        }

        return BuildFrom(ParseQuery(parsed.Query), out request, out error);
    }

    /// <summary>Parses the <c>register --app-id ... --name ... --target ...</c> CLI argument list.</summary>
    public static bool TryParseArguments(IReadOnlyList<string> args, [NotNullWhen(true)] out RegistrationRequest? request, out string error)
    {
        request = null;
        if (args.Count == 0 || !string.Equals(args[0], RegisterAction, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Not a '{RegisterAction}' command.";
            return false;
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected argument '{token}'; expected an option like --app-id.";
                return false;
            }

            // Accept both --key=value (unambiguous, needed for values with leading dashes) and --key value.
            var body = token[2..];
            var eq = body.IndexOf('=', StringComparison.Ordinal);
            if (eq >= 0)
            {
                fields[Canonical(body[..eq])] = body[(eq + 1)..];
                continue;
            }

            var key = Canonical(body);
            if (key == "supportsminimized")
            {
                fields[key] = bool.TrueString;
                continue;
            }

            // A value is the next token. It normally must not look like another option, so a forgotten
            // value is caught - except free-form options (--args) whose value may itself start with "--".
            var hasValue = i + 1 < args.Count &&
                (ConsumesRawValue(key) || !args[i + 1].StartsWith("--", StringComparison.Ordinal));
            if (!hasValue)
            {
                error = $"Option '{token}' expects a value.";
                return false;
            }

            fields[key] = args[++i];
        }

        return BuildFrom(fields, out request, out error);
    }

    private static bool BuildFrom(IReadOnlyDictionary<string, string> fields, [NotNullWhen(true)] out RegistrationRequest? request, out string error)
    {
        request = null;

        var appId = Value(fields, "appid");
        var name = Value(fields, "name");
        var target = Value(fields, "target");

        if (string.IsNullOrWhiteSpace(appId)) { error = "Registration is missing 'appId'."; return false; }
        if (string.IsNullOrWhiteSpace(name)) { error = "Registration is missing 'name'."; return false; }
        if (string.IsNullOrWhiteSpace(target)) { error = "Registration is missing 'target'."; return false; }

        request = new RegistrationRequest
        {
            AppId = appId.Trim(),
            Name = name.Trim(),
            Target = target.Trim(),
            Arguments = Value(fields, "args"),
            Icon = Value(fields, "icon"),
            Publisher = Value(fields, "publisher"),
            SuggestedProfile = Value(fields, "suggestedprofile"),
            SupportsMinimized = ParseBool(Value(fields, "supportsminimized")),
        };
        error = "";
        return true;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.IndexOf('=', StringComparison.Ordinal);
            if (split < 0)
            {
                fields[Canonical(WebUtility.UrlDecode(pair))] = "";
                continue;
            }

            var key = Canonical(WebUtility.UrlDecode(pair[..split]));
            fields[key] = WebUtility.UrlDecode(pair[(split + 1)..]);
        }
        return fields;
    }

    // Fold both transports' key spellings (appId, app-id, ARGS, ...) onto one canonical lookup key.
    private static string Canonical(string key) => key.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    // Free-form options whose space-separated value may itself begin with "--" (e.g. launch arguments).
    private static bool ConsumesRawValue(string canonicalKey) => canonicalKey is "args";

    private static string? Value(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static bool ParseBool(string? value) =>
        value is not null && (bool.TryParse(value, out var b) ? b : value is "1" or "yes" or "on");
}
