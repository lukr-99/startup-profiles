namespace StartupProfiles.App.Api;

/// <summary>Uniform result shape for operation endpoints: { ok, output?, error? }.</summary>
public sealed record OperationResult(bool Ok, string? Output = null, string? Error = null);
