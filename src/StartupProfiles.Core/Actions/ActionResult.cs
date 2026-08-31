namespace StartupProfiles.Core.Actions;

/// <summary>Outcome of a single action, mirroring the { ok, output, error } shape of the local API.</summary>
public sealed record ActionResult(bool Success, string? Output = null, string? Error = null)
{
    public static ActionResult Ok(string? output = null) => new(true, output);
    public static ActionResult Fail(string error) => new(false, null, error);
}
