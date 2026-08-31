using System.Diagnostics.CodeAnalysis;
using StartupProfiles.Core.Integration;

namespace StartupProfiles.App.Integration;

/// <summary>
/// Recognises and parses the two ways an external app triggers registration on the command line: the
/// protocol handler passes a single <c>startupprofiles://register?...</c> URI, and the CLI form is
/// <c>StartupProfiles.exe register --app-id ...</c>. Both defer to the Core parser.
/// </summary>
internal static class RegistrationLaunch
{
    /// <summary>True when these args are a registration invocation (so the host runs the one-shot flow).</summary>
    public static bool IsRegistrationInvocation(IReadOnlyList<string> args) =>
        args.Count > 0 &&
        (args[0].StartsWith($"{RegistrationRequestParser.Scheme}:", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], RegistrationRequestParser.RegisterAction, StringComparison.OrdinalIgnoreCase));

    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out RegistrationRequest? request, out string error)
    {
        if (args.Count > 0 && args[0].StartsWith($"{RegistrationRequestParser.Scheme}:", StringComparison.OrdinalIgnoreCase))
            return RegistrationRequestParser.TryParseUri(args[0], out request, out error);

        return RegistrationRequestParser.TryParseArguments(args, out request, out error);
    }
}
