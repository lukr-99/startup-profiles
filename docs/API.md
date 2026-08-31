# Startup Profiles local API

> Implemented (Milestone 3), except `POST /api/register`, which returns 501 until Milestone 5.

The configuration UI and any local agent talk to a **loopback-only** HTTP server bound to
`127.0.0.1` (mirroring Treeline). When running, the app writes its live URL to a discovery
file:

- Windows: `%APPDATA%\StartupProfiles\endpoint.json`

All request/response bodies are JSON. Operation endpoints return
`{ "ok": bool, "output": string?, "error": string? }`.

## Endpoints

| Method | Path | Description |
| --- | --- | --- |
| GET | `/api/health` | Status, version, data dir, profile count. |
| GET | `/api/profiles` | All profiles as summaries (id, name, icon, action count). |
| GET | `/api/profiles/{id}` | One full profile including its actions (404 if unknown). |
| POST | `/api/profiles` | Create a profile (body is a full profile; 409 if the id exists). |
| PUT | `/api/profiles/{id}` | Create or replace the profile with this id. |
| DELETE | `/api/profiles/{id}` | Delete a profile (two-phase confirm, see below). |
| POST | `/api/profiles/{id}/run` | Execute a profile now, record history, return the run. |
| GET | `/api/history` | Recent execution runs (`?take=` to limit). |
| POST | `/api/register` | Integration registration (see INTEGRATION.md). Returns 501 for now. |

All bodies are JSON, camelCase, with enums as camelCase strings.

Deleting a profile follows a single-use, server-issued confirmation token. `DELETE /api/profiles/{id}`
without a token returns `{ required: true, confirmToken, summary }`; resubmit as
`DELETE /api/profiles/{id}?confirmToken=<token>` to actually delete. The token is bound to the exact
action, single-use, and expires quickly, so an agent cannot skip the prompt.
