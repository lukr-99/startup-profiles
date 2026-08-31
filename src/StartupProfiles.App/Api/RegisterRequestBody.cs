using StartupProfiles.Core.Integration;

namespace StartupProfiles.App.Api;

/// <summary>
/// Request body for <c>POST /api/register</c>: the requesting app's metadata plus the profiles the
/// caller wants it added to. Fields are nullable so a malformed body yields a clean validation error
/// instead of a deserialization failure; <see cref="ToRequest"/> maps a validated body to the Core type.
/// </summary>
public sealed record RegisterRequestBody(
    string? AppId,
    string? Name,
    string? Target,
    string? Arguments = null,
    string? Icon = null,
    string? Publisher = null,
    string? SuggestedProfile = null,
    bool SupportsMinimized = false,
    string[]? ProfileIds = null)
{
    public RegistrationRequest ToRequest() => new()
    {
        AppId = AppId!.Trim(),
        Name = Name!.Trim(),
        Target = Target!.Trim(),
        Arguments = Arguments,
        Icon = Icon,
        Publisher = Publisher,
        SuggestedProfile = SuggestedProfile,
        SupportsMinimized = SupportsMinimized,
    };
}
