# Handoff

Snapshot of Windows Startup Profiles as of 2026-08-31. For the full design see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); for conventions see [AGENTS.md](AGENTS.md) and
[CONTRIBUTING.md](CONTRIBUTING.md).

## Status: Milestones 1-5 + installer done; UI polish + conditions next

The app runs today: launch it and a WPF login selector appears; pick a context and it launches that
profile's actions, then lives in the tray. Profiles are editable in the config window. A loopback HTTP
API and a bundled agent skill expose the same data to agents. External apps can *request* to be added
to a profile via the integration contract (protocol / CLI / API), and the user approves in a Startup
Profiles-owned confirmation window.

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

## Milestone 5 - integration contract (done)

`Core/Integration` (portable) + `App/Integration` (host) implement the contract from
[docs/INTEGRATION.md](docs/INTEGRATION.md). `RegistrationRequestParser` parses both the
`startupprofiles://register?...` URI and the `register --app-id ...` CLI form; `ProfileRegistrar`
appends a launch action to the chosen profiles (idempotent per target). Three entry points feed one
trusted, Startup Profiles-owned confirmation window: the protocol, the CLI, and `POST /api/register`
(two-phase confirm, like delete). A one-shot `register` invocation routes its write through a running
instance's loopback API when one is live, so the running app stays the single writer of `profiles.json`.

One deliberate gap: **registering the `startupprofiles://` scheme with Windows is left to the installer**
(below). Until then, invoke the exe with the URI directly. `supportsMinimized` is carried as metadata
only - launch-minimized is not yet an action field.

## Installer (done)

`install/install.ps1` installs for the current user (no elevation): publishes the App (self-contained
by default, `-FrameworkDependent` for the smaller build), installs to
`%LOCALAPPDATA%\Programs\StartupProfiles`, adds a Start Menu shortcut, registers the launcher at login,
registers the `startupprofiles://` protocol, and installs the agent skill to `~/.claude/skills`.
Login/protocol registration is done by invoking the app's own maintenance commands
(`StartupProfiles.exe --register-login` / `--register-protocol`), which run the
`IStartupRegistration` / `IProtocolRegistration` Windows adapters. `install/uninstall.ps1` reverses it
all (registry keys removed directly so it works even if the exe is gone; `-PurgeData` also deletes
`%APPDATA%\StartupProfiles`). Switches: `-Port`, `-FrameworkDependent`, `-NoStartup`, `-NoProtocol`,
`-NoSkill`.

## Next

1. **UI / visual polish**: the launcher and config windows apply the color palette but have **no control
   styles** - every `Button`/`TextBox`/`ComboBox`/`ListBox`/`DataGrid` uses WPF's default templates, so
   the app looks unstyled. Add implicit `Style`s (flat borders, padding, rounded corners, hover/focus)
   to the theme dictionaries for both light and dark, so it reads as the "polished system dialog" the
   architecture calls for. `dotnetlib` is the style reference. Not a bug - a missing layer.
2. **Profile conditions**: `ProfileCondition`/`ConditionType` are stored but not evaluated.
3. Optional: an app icon (`ApplicationIcon` and a real tray icon), and revisiting the dotnetlib
   dependency once a shared feed exists.
