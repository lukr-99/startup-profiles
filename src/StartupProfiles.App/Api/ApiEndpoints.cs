using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StartupProfiles.Core.Confirmations;
using StartupProfiles.Core.Execution;
using StartupProfiles.Core.Models;
using StartupProfiles.Core.Storage;

namespace StartupProfiles.App.Api;

/// <summary>Maps the loopback HTTP API (docs/API.md), consumed by the config UI and by agents.</summary>
public static class ApiEndpoints
{
    public static void MapStartupProfilesApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", (IProfileStore profiles) => Results.Ok(new
        {
            status = "ok",
            version = typeof(ApiEndpoints).Assembly.GetName().Version?.ToString(),
            dataDirectory = StartupProfilesPaths.DataDirectory,
            profiles = profiles.GetAll().Count,
        }));

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

        // Integration registration is Milestone 5 (see docs/INTEGRATION.md).
        api.MapPost("/register", () =>
            Results.Json(new OperationResult(false, null, "Registration is not implemented yet."), statusCode: StatusCodes.Status501NotImplemented));
    }

    private static bool IsValid(Profile profile, out string error)
    {
        if (string.IsNullOrWhiteSpace(profile.Id)) { error = "Profile id is required."; return false; }
        if (string.IsNullOrWhiteSpace(profile.Name)) { error = "Profile name is required."; return false; }
        error = "";
        return true;
    }
}
