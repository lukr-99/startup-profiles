---
name: startup-profiles
description: Inspect and run Windows startup profiles through the Startup Profiles app's local HTTP API. Use when the user wants to list their startup profiles, see what an environment/context profile launches, run a profile (Work/Dev/School/Games/Chill/Everything), or review execution history/failed actions. Startup Profiles must be running.
---

# Startup Profiles agent skill

Startup Profiles runs a local-only HTTP API (bound to `127.0.0.1`) exposing the user's context
startup profiles, their actions, and execution history. Use it instead of guessing which apps a
context launches. An app can also request to be added to a profile via `POST /api/register`
(two-phase confirm, like delete - see [docs/INTEGRATION.md](../../docs/INTEGRATION.md)).

## 1. Discover the endpoint

When running, Startup Profiles writes its URL to a discovery file:

- Windows: `%APPDATA%\StartupProfiles\endpoint.json`

```powershell
$URL = (Get-Content "$env:APPDATA\StartupProfiles\endpoint.json" | ConvertFrom-Json).url
```

If the file is missing, the app is not running - tell the user to start it rather than
shelling out.

## 2. Operations

See [docs/API.md](../../docs/API.md) for the full endpoint table. Highlights:

- `GET /api/profiles` - list profiles (Work, Dev, School, Games, Chill, Everything, ...).
- `GET /api/profiles/{id}` - a profile's ordered actions.
- `POST /api/profiles/{id}/run` - run a profile now; returns the run with per-action results.
- `GET /api/base` - the base: actions that run before every profile with `includeBase` on (the
  default). Include them when describing what a profile launches.
- `GET /api/library` - the global library. An action with `libraryItemId` starts that item's current
  target and arguments, so look the item up rather than trusting the action's stored copy.
- `GET /api/history` - recent runs and failed actions.

Deleting a profile is two-phase: `DELETE /api/profiles/{id}` returns a `confirmToken`; resubmit as
`DELETE /api/profiles/{id}?confirmToken=<token>` to actually delete.

## 3. Safety

Running a profile launches real applications, scripts, and services. Confirm with the user before
`POST .../run`. Never skip the delete confirmation token by issuing and consuming it in one step
without showing the user the returned `summary` first.
