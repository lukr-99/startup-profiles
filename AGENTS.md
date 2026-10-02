# AGENTS

Repository-specific guidance for coding agents working on Windows Startup Profiles.

## Required context

1. Read [ARCHITECTURE.md](ARCHITECTURE.md) before changing module seams or dependency direction;
   its "Implementation status" section is the source of truth for what exists.
2. Read [docs/API.md](docs/API.md) and [docs/INTEGRATION.md](docs/INTEGRATION.md) before touching the
   loopback API or the registration contract.
3. Follow the shared rules and reference repos noted in [CONTRIBUTING.md](CONTRIBUTING.md).

## Baseline

- One top-level type per file, named for the type.
- `StartupProfiles.Core` (net10.0) stays portable: no UI, ASP.NET, or platform APIs. Platform code
  lives in `StartupProfiles.Windows` behind Core seams (`IProcessLauncher`, `IStartupRegistration`,
  `IServiceController`, `IVpnConnector`).
- Constructor injection; one explicit composition root per host. `WindowsRuntime` composes the
  Windows action registry today; the App host will own the full graph.
- Profiles are declarative data (`Profile` -> `ProfileAction[]`), never hardcoded logic. A new action
  type means a new `IActionHandler`, not a change to `ProfileRunner`.
- Isolate the clock, delays, filesystem, and process launching behind seams; add or update
  deterministic tests with every behavior change.
- Conventional Commits, one coherent change per commit. No AI-attribution trailer.

## Verification

```powershell
dotnet format StartupProfiles.slnx --verify-no-changes
dotnet build StartupProfiles.slnx -c Release
dotnet test StartupProfiles.slnx -c Release
```
