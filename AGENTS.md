# AGENTS

Repository-specific guidance for coding agents working on Windows Startup Profiles.

## Required context

1. Read [ARCHITECTURE.md](ARCHITECTURE.md) before changing module seams or dependency direction;
   its "Implementation status" section is the source of truth for what exists.
2. Read [docs/API.md](docs/API.md) and [docs/INTEGRATION.md](docs/INTEGRATION.md) before touching the
   loopback API or the registration contract.
3. Read [CONTEXT.md](CONTEXT.md) before introducing domain terms.
4. Follow the shared rules and reference repos noted in [CONTRIBUTING.md](CONTRIBUTING.md).

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

## Pitfalls

- `docs/pitfalls.md` lists mistakes this repository already made. When something fails in a way you
  did not expect, search it and CodePrint's `docs/pitfalls/` for the error text before debugging.
  CodePrint usually sits beside this repository; find it by name if it does not.
- When a bug took longer to find than to fix, came back, or came from a tool trap, add an entry in
  the same commit as the fix. Add a test or check that catches it when you can.
- If it could hit another repository, report it as a `pitfall` item on the CodePrint project in
  GoalMaker (CodePrint's `docs/pitfalls/README.md`). Do not edit CodePrint from this repository.

## Tracking

- This repository's board is its GoalMaker project: `find_project` with this folder.
- Move an item to Doing when you start it. Put bugs and ideas you find but do not fix on the board.
- A pull request lists its items as `GoalMaker: <item id>` lines, and items reach Done only when the
  work is merged to `main` (CodePrint's `docs/project-tracking.md`).

## Verification

`tools/validate_repository.py` is a copy of CodePrint's validator, so CI can run it. Refresh it from
CodePrint when CodePrint changes it.

```powershell
python tools/validate_repository.py --root .
dotnet format StartupProfiles.slnx --verify-no-changes
dotnet build StartupProfiles.slnx -c Release
dotnet test StartupProfiles.slnx -c Release
```
