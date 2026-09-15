using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StartupProfiles.Core.Confirmations;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Integration;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Api;

/// <summary>Maps the loopback HTTP API (docs/API.md), consumed by the config UI and by agents.</summary>
public static class ApiEndpoints
{
    public static void MapStartupProfilesApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", (IProfileStore profiles, IBaseStore baseStore) => Results.Ok(new
        {
            status = "ok",
            version = typeof(ApiEndpoints).Assembly.GetName().Version?.ToString(),
            dataDirectory = StartupProfilesPaths.DataDirectory,
            profiles = profiles.GetAll().Count,
            baseActions = baseStore.Load().Actions.Count,
        }));

        // The base: actions that run before every profile with includeBase on. Not a profile itself.
        api.MapGet("/base", (IBaseStore baseStore) => Results.Ok(baseStore.Load()));

        api.MapPut("/base", (StartupBase value, IBaseStore baseStore) =>
        {
            baseStore.Save(value);
            return Results.Ok(value);
        });

        api.MapPost("/base/run", async (ProfileExecutor executor) =>
            Results.Ok(await executor.RunBaseAndRecordAsync()));

        api.MapGet("/profiles", (IProfileStore profiles) =>
            Results.Ok(profiles.GetAll().Select(ProfileSummary.From)));

        api.MapGet("/profiles/{id}", (string id, IProfileStore profiles) =>
            profiles.Find(id) is { } profile ? Results.Ok(profile) : Results.NotFound());

        api.MapPost("/profiles", (Profile profile, IProfileStore profiles) =>
        {
            if (!IsValid(profile, out var error)) return Results.BadRequest(new OperationResult(false, null, error));
            if (profiles.Find(profile.Id) is not null)
                return Results.Conflict(new OperationResult(false, null, $"A profile with id '{profile.Id}' already exists."));
            profiles.Save(profile);
            return Results.Created($"/api/profiles/{profile.Id}", profile);
        });

        api.MapPut("/profiles/{id}", (string id, Profile profile, IProfileStore profiles) =>
        {
            var target = profile with { Id = id };
            if (!IsValid(target, out var error)) return Results.BadRequest(new OperationResult(false, null, error));
            profiles.Save(target);
            return Results.Ok(target);
        });

        // Deleting a profile is destructive (two-phase confirm): first call issues a token, second consumes it.
        api.MapDelete("/profiles/{id}", (string id, string? confirmToken, IProfileStore profiles, ConfirmationService confirm) =>
        {
            var profile = profiles.Find(id);
            if (profile is null) return Results.NotFound();

            var signature = ConfirmationService.Signature("profile.delete", id);
            if (!confirm.TryConsume(confirmToken, signature))
                return Results.Ok(new ConfirmationRequired(true, confirm.Issue(signature), $"Delete profile '{profile.Name}'."));

            profiles.Remove(id);
            return Results.Ok(new OperationResult(true));
        });

        api.MapPost("/profiles/{id}/run", async (string id, IProfileStore profiles, ProfileExecutor executor) =>
        {
            var profile = profiles.Find(id);
            if (profile is null) return Results.NotFound();
            return Results.Ok(await executor.RunAndRecordAsync(profile));
        });

        api.MapGet("/history", (int? take, IHistoryStore history) =>
            Results.Ok(history.GetRecent(take ?? 50)));

        // Integration registration (see docs/INTEGRATION.md). Startup Profiles owns the decision, so an
        // agent cannot add an app silently: the first call previews the change and issues a single-use
        // token; the app is only added when that token is resubmitted (same two-phase rule as delete).
        api.MapPost("/register", (RegisterRequestBody? body, string? confirmToken, IProfileRegistrar registrar, ConfirmationService confirm) =>
        {
            if (body is null) return Reject("A registration body is required.");
            if (string.IsNullOrWhiteSpace(body.AppId)) return Reject("Registration is missing 'appId'.");
            if (string.IsNullOrWhiteSpace(body.Name)) return Reject("Registration is missing 'name'.");
            if (string.IsNullOrWhiteSpace(body.Target)) return Reject("Registration is missing 'target'.");

            var profileIds = body.ProfileIds ?? [];
            if (profileIds.Length == 0) return Reject("Choose at least one profile to add the app to.");

            var signature = ConfirmationService.Signature("integration.register", RegisterSignature(body.AppId, body.Target, profileIds));
            if (!confirm.TryConsume(confirmToken, signature))
                return Results.Ok(new ConfirmationRequired(true, confirm.Issue(signature),
                    $"Add '{body.Name}' to: {string.Join(", ", profileIds)}."));

            var outcome = registrar.Apply(body.ToRequest(), profileIds);
            return Results.Ok(new RegistrationResponse(true, outcome, SummarizeRegistration(outcome)));
        });
    }

    private static IResult Reject(string error) => Results.BadRequest(new OperationResult(false, null, error));

    // Bind the confirmation token to the exact app and destination profiles so it cannot be replayed
    // to register a different app, or the same app into profiles the caller never previewed.
    private static string RegisterSignature(string appId, string target, IEnumerable<string> profileIds) =>
        $"{appId}|{target}|{string.Join(',', profileIds.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))}";

    private static string SummarizeRegistration(RegistrationOutcome outcome)
    {
        var parts = new List<string>();
        if (outcome.AddedTo.Count > 0) parts.Add($"added to {string.Join(", ", outcome.AddedTo)}");
        if (outcome.AlreadyPresentIn.Count > 0) parts.Add($"already in {string.Join(", ", outcome.AlreadyPresentIn)}");
        if (outcome.UnknownProfileIds.Count > 0) parts.Add($"unknown: {string.Join(", ", outcome.UnknownProfileIds)}");
        return parts.Count > 0 ? string.Join("; ", parts) : "No changes.";
    }

    private static bool IsValid(Profile profile, out string error)
    {
        if (string.IsNullOrWhiteSpace(profile.Id)) { error = "Profile id is required."; return false; }
        if (string.IsNullOrWhiteSpace(profile.Name)) { error = "Profile name is required."; return false; }
        error = "";
        return true;
    }
}
