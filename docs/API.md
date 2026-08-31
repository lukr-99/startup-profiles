# Startup Profiles local API

> Stub. The loopback API is not implemented yet; this file records the intended shape so the
> `App/Api` layer and the agent skill can be built against it.

The configuration UI and any local agent talk to a **loopback-only** HTTP server bound to
`127.0.0.1` (mirroring Treeline). When running, the app writes its live URL to a discovery
file:

- Windows: `%APPDATA%\StartupProfiles\endpoint.json`

All request/response bodies are JSON. Operation endpoints return
`{ "ok": bool, "output": string?, "error": string? }`.

## Planned endpoints

| Method | Path | Description |
| --- | --- | --- |
| GET | `/api/health` | Status, version, data dir, profile count. |
| GET | `/api/profiles` | All profiles (id, name, icon, action count). |
| GET | `/api/profiles/{id}` | One full profile including its actions. |
| POST | `/api/profiles` | Create a profile. |
| PUT | `/api/profiles/{id}` | Update a profile. |
| DELETE | `/api/profiles/{id}` | Delete a profile. |
| POST | `/api/profiles/{id}/run` | Execute a profile now (used by the launcher and tray). |
| GET | `/api/history` | Recent execution runs and failed actions. |
| POST | `/api/register` | Handle an integration registration request (see INTEGRATION.md). |

Destructive operations follow the Treeline pattern: an explicit UI confirmation plus a
single-use, server-issued confirmation token, enforced on the API too so agents cannot skip
the prompt.
