# Security

## Supported versions

Startup Profiles is pre-release (0.x); only the latest `main` receives fixes.

## Reporting

Report suspected vulnerabilities privately to the maintainer via a GitHub security advisory on
`lukr-99/startup-profiles`. Do not open a public issue describing an active vulnerability.

## Trust boundaries

- **Local data** - profiles, config, and run history are plain JSON under
  `%APPDATA%\StartupProfiles`. Nothing is sent off the machine. Treat these files as trusted input
  from the user; corrupt or hand-edited files fall back to defaults rather than crashing.
- **Action execution** - running a profile launches real applications, scripts, and services that the
  user configured. Profiles are authored locally by the user, not received from any peer. `RunScript`
  runs through `cmd.exe`; treat profile contents as user-trusted, not remote input.
- **VPN and services** - the VPN adapter uses `rasdial` with credentials already saved in Windows;
  credentials are never taken from a profile or passed on the command line. Service control is limited
  to starting a named service.
- **Loopback API (planned, M3)** - the config API will bind to `127.0.0.1` only, and destructive
  operations will require an explicit, server-issued confirmation token. See [docs/API.md](docs/API.md).
- **Integration contract (planned, M5)** - external apps may only *request* registration; Startup
  Profiles owns the confirmation UI and all configuration authority. See
  [docs/INTEGRATION.md](docs/INTEGRATION.md). Registration requests are untrusted input and are
  validated before they can change state.

## Recovery

Delete or edit the JSON files under `%APPDATA%\StartupProfiles` to reset profiles, config, or history.
Startup registration is a per-user `Run` key value that can be removed from the app or via
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
