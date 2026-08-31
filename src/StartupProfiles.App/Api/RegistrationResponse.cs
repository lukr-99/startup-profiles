using StartupProfiles.Core.Integration;

namespace StartupProfiles.App.Api;

/// <summary>
/// Success response for the confirmed phase of <c>POST /api/register</c>: the uniform <c>ok</c> flag,
/// the structured <see cref="RegistrationOutcome"/> for programmatic callers, and a human summary.
/// </summary>
public sealed record RegistrationResponse(bool Ok, RegistrationOutcome Outcome, string Output);
