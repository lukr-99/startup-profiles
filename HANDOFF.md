# Handoff

Snapshot of Windows Startup Profiles as of 2026-08-31. For the full design see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); for conventions see [AGENTS.md](AGENTS.md) and
[CONTRIBUTING.md](CONTRIBUTING.md).

## Status: Milestones 1-4 done; Milestone 5 (integration contract) not started

The app runs today: launch it and a WPF login selector appears; pick a context and it launches that
profile's actions, then lives in the tray. Profiles are editable in the config window. A loopback HTTP
API and a bundled agent skill expose the same data to agents.

## Build, test, run

```powershell
dotnet build StartupProfiles.slnx -c Release
dotnet test StartupProfiles.slnx -c Release
dotnet format StartupProfiles.slnx --verify-no-changes
```

- Build is clean (0 warnings, warnings-as-errors on) and 37 tests pass.
- Run the app: `dotnet run --project src/StartupProfiles.App` (add `--headless` for the API only,
  `--port N` to override the port, default 8790).
- Data lives in `%APPDATA%\StartupProfiles` (`profiles.json`, `config.json`, `history.json`,
  `endpoint.json`). Nothing leaves the machine.

## What exists

| Project | TFM | Contents |
| --- | --- | --- |
| `StartupProfiles.Core` | net10.0 | Models, action handlers + registry, `ProfileRunner`/`ProfileExecutor`, JSON stores, `ConfirmationService`, and the Windows seam interfaces. Portable and unit-tested. |
| `StartupProfiles.Windows` | net10.0-windows | Windows adapters: startup registration (HKCU Run key), service control, VPN (`rasdial`), and the action-registry composition root. |
| `StartupProfiles.App` | net10.0-windows | WPF launcher + config UI, WinForms tray, and the loopback ASP.NET Core API + `endpoint.json`. |
| `tests/*` | net10.0(-windows) | xUnit: Core behavior, the Windows registry round-trip, HTTP API, and WPF view models. |

Key design points:

- Profiles are declarative data (`Profile` -> `ProfileAction[]`) run by `ProfileRunner`; a new action
  type is a new `IActionHandler`, registered in `ActionHandlerRegistry`.
- Platform code (process launch, services, VPN, registry, clock/delays) sits behind Core seams so the
  engine is deterministic in tests. The Windows adapters live outside portable Core.
- The WPF UI talks to Core in-process; the loopback API is for agents/external tools. Deleting a
  profile requires a single-use confirmation token on the API.

## Decisions a new session should know

- UI is **WPF**, not the WebView2 approach the original docs named. See
  [docs/adr/0001-local-wpf-mvvm.md](docs/adr/0001-local-wpf-mvvm.md). Local `Mvvm/` mirrors dotnetlib;
  no cross-repo dependency (no shared NuGet feed).
- The App pins `RuntimeFrameworkVersion=10.0.7` to work around a dev-box SDK/runtime mismatch (the WPF
  markup-compile helper wanted 10.0.9). Remove the pin when the box has a matching runtime.
- Conventions come from the owner's `CodePrint` (rules) and `dotnetlib` (.NET architecture) repos, with
  `Relay`/`MicForge` as reference implementations. One top-level type per file; Conventional Commits;
  no AI-attribution trailer; PolyForm Noncommercial license.

## Next

1. **Milestone 5 - integration contract** (`Core/Integration` + App): the `startupprofiles://register`
   protocol handler and `StartupProfiles.exe register ...` CLI, feeding an app-owned confirmation UI.
   `POST /api/register` currently returns 501 as the placeholder. See [docs/INTEGRATION.md](docs/INTEGRATION.md).
2. **Installer** (`install/install.ps1` is still a placeholder): publish, install to
   `%LOCALAPPDATA%\Programs`, register the launcher at login via `IStartupRegistration`, register the
   protocol, and install the agent skill.
3. **Profile conditions**: `ProfileCondition`/`ConditionType` are stored but not evaluated.
4. Optional: an app icon (`ApplicationIcon` and a real tray icon), and revisiting the dotnetlib
   dependency once a shared feed exists.
