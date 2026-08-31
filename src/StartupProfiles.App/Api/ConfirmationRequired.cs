namespace StartupProfiles.App.Api;

/// <summary>
/// Phase-1 response for a destructive operation: carries a single-use token the caller must resubmit
/// (after showing <see cref="Summary"/> to the user) to actually perform the action.
/// </summary>
public sealed record ConfirmationRequired(bool Required, string ConfirmToken, string Summary);
