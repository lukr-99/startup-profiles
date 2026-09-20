# Startup Profiles local API

> Implemented (Milestone 3); `POST /api/register` landed with Milestone 5.

The configuration UI and any local agent talk to a **loopback-only** HTTP server bound to
`127.0.0.1` (mirroring Treeline). When running, the app writes its live URL to a discovery
file:

- Windows: `%APPDATA%\StartupProfiles\endpoint.json`

All request/response bodies are JSON. Operation endpoints return
`{ "ok": bool, "output": string?, "error": string? }`.

## Endpoints

| Method | Path | Description |
| --- | --- | --- |
| GET | `/api/health` | Status, version, data dir, profile count, base action count. |
| GET | `/api/base` | The base: `{ actions: [...] }`, run before every profile with `includeBase` on. |
| PUT | `/api/base` | Replace the base (body `{ actions: [...] }`). |
| POST | `/api/base/run` | Run only the base now, record history (as profile id `base`), return the run. |
| GET | `/api/library` | The global library: startable items (`id, name, type, target, arguments, runAsAdmin`). |
| POST | `/api/library` | Add an item; its `id` is derived from `name` (400 without a name and target). |
| PUT | `/api/library/{id}` | Create or replace the item with this id; linked actions start the new values. |
| DELETE | `/api/library/{id}` | Delete an item (two-phase confirm); actions linked to it become standalone copies. |
| GET | `/api/profiles` | All profiles as summaries (id, name, icon, action count, includeBase). |
| GET | `/api/profiles/{id}` | One full profile including its actions (404 if unknown). |
| POST | `/api/profiles` | Create a profile (body is a full profile; 409 if the id exists). |
| PUT | `/api/profiles/{id}` | Create or replace the profile with this id. |
| DELETE | `/api/profiles/{id}` | Delete a profile (two-phase confirm, see below). |
| POST | `/api/profiles/{id}/run` | Execute a profile now, record history, return the run. |
| GET | `/api/history` | Recent execution runs (`?take=` to limit). |
| POST | `/api/register` | Request that an app be added to profiles and/or the base (two-phase confirm; see INTEGRATION.md). |

All bodies are JSON, camelCase, with enums as camelCase strings.

The **base** is not a profile: it has no id, name, or launcher tile, only actions. Running a profile
(`POST /api/profiles/{id}/run`, the launcher, the tray, or the config window) runs the base actions
first, as part of the same run, unless the profile has `"includeBase": false`. Profiles without the
field include the base.

An action with `"libraryItemId"` links to a **library item**: when it runs, the item supplies `type`,
`target`, `arguments`, and `runAsAdmin` (its own `delay`, `failureBehaviour`, and `retryCount` still
apply). The action's own values are the last-saved copy and are used if the item has been deleted.

Deleting a profile follows a single-use, server-issued confirmation token. `DELETE /api/profiles/{id}`
without a token returns `{ required: true, confirmToken, summary }`; resubmit as
`DELETE /api/profiles/{id}?confirmToken=<token>` to actually delete. The token is bound to the exact
action, single-use, and expires quickly, so an agent cannot skip the prompt.

## Registration

`POST /api/register` lets a local agent or app request that an application be added to one or more
profiles, to the base, or to neither. Startup Profiles owns the decision, so it uses the same two-phase
confirmation as delete: an app is never added silently.

Request body:

```json
{
  "appId": "com.example.app",
  "name": "Example App",
  "target": "C:\\Apps\\example.exe",
  "arguments": "--fast",
  "publisher": "Example Inc",
  "suggestedProfile": "dev",
  "supportsMinimized": true,
  "profileIds": ["dev", "games"],
  "includeBase": false
}
```

`appId`, `name`, and `target` are required; `suggestedProfile` is a hint only and is not applied on its
own. `profileIds` and `includeBase` say where the app should land - an empty `profileIds` with
`includeBase: false` is the "just recognize" case, which only keeps the app in the library. The first
call returns `{ required: true, confirmToken, summary }` with a human-readable summary of the change and
adds nothing. Resubmit the **same body** as `POST /api/register?confirmToken=<token>` to apply it; the
response is `{ ok: true, outcome, output }`, where `outcome` carries `addedTo`, `alreadyPresentIn`,
`unknownProfileIds`, `libraryItemId`, `addedToLibrary`, `addedToBase`, and `alreadyInBase`, and `output`
says the same in one line. The token is bound to the app and the exact destinations (profiles *and* the
base flag), is single-use, and expires quickly, so it cannot be replayed against a different app or a
different destination set.

Every registration goes through the library: the app is kept as a library item (reusing the item that
already starts the same thing), and each action added links to it via `libraryItemId`. Adding the same
target to the same destination twice is idempotent (reported as "already in"), and a profile that
includes the base is skipped when the base already starts the app, so it never launches twice.
