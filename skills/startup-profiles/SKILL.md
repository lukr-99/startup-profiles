---
name: startup-profiles
description: Inspect and run Windows startup profiles through the Startup Profiles app's local HTTP API. Use when the user wants to list their startup profiles, see what an environment/context profile launches, run a profile (Work/Dev/School/Games/Chill/Everything), review execution history/failed actions, or register an application with Startup Profiles. Startup Profiles must be running. NOTE: skeleton - the API is not implemented yet.
---

# Startup Profiles agent skill

> Skeleton. The app and its loopback API are not implemented yet, so this skill only
> describes the intended surface. Do not attempt live calls until the API exists.

Startup Profiles will run a local-only HTTP API (bound to `127.0.0.1`) exposing the user's
context startup profiles, their actions, execution history, and the registration contract.
Use it instead of guessing which apps a context launches.

## 1. Discover the endpoint

When running, Startup Profiles writes its URL to a discovery file:

- Windows: `%APPDATA%\StartupProfiles\endpoint.json`

```powershell
$URL = (Get-Content "$env:APPDATA\StartupProfiles\endpoint.json" | ConvertFrom-Json).url
```

If the file is missing, the app is not running - tell the user to start it rather than
shelling out.

## 2. Planned operations

See [docs/API.md](../../docs/API.md) for the endpoint table. Highlights:

- `GET /api/profiles` - list profiles (Work, Dev, School, Games, Chill, Everything, ...).
- `GET /api/profiles/{id}` - a profile's ordered actions.
- `POST /api/profiles/{id}/run` - run a profile now.
- `GET /api/history` - recent runs and failed actions.
- `POST /api/register` - integration registration request (see docs/INTEGRATION.md).

## 3. Safety

Running a profile launches real applications, scripts, and services. Confirm with the user
before `POST .../run`. Registration always goes through the app's own confirmation UI - the
skill supplies a request, never final configuration.
