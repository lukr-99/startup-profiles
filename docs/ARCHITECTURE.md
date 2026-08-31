# Architecture

> Design document. Milestones 1-5 are implemented: the Core domain/storage/execution engine, the
> Windows platform adapters, the App host (loopback API + `endpoint.json` discovery + tray), the
> WPF launcher and config UIs, and the integration contract (protocol/CLI/API + trusted confirmation
> window). See [Implementation status](#implementation-status) for exactly what exists today.
>
> Note: this design originally called for a WebView2 UI over the loopback server (the Treeline
> pattern). Milestone 4 switched to a native **WPF** UI talking to Core in-process; the loopback API
> remains for agents. See [docs/adr/0001-local-wpf-mvvm.md](adr/0001-local-wpf-mvvm.md).

## Guiding principle

This is a **Windows environment orchestration** tool, not another bloated startup manager.
Its whole job is:

> Windows starts -> one lightweight launcher appears -> the user chooses context -> the
> correct environment starts.

Two hard constraints follow from that:

1. **The launcher stays minimal and fast.** It looks and behaves like a polished system
   dialog. All rich editing lives in a separate configuration UI.
2. **Profiles are data-driven.** A profile is declarative data (`Profile` -> `ProfileAction[]`),
   never hardcoded logic. This makes profiles a small startup *workflow*.

## Project layout

```
StartupProfiles.slnx
  src/StartupProfiles.Core   net10.0            portable, no Windows Forms/ASP.NET deps
    Models/        Profile, ProfileAction, ActionType, FailureBehaviour, conditions
    Actions/       one handler per action type (launch, url, script, service, ...)
    Execution/     ProfileRunner: ordering, delays, wait-for, failure handling, history
    Storage/       load/save profiles + config as JSON under %APPDATA%\StartupProfiles
    Windows/       startup registration, process handling, services, elevation helpers
    Integration/   registration-request parsing + the confirmation model
  src/StartupProfiles.Windows net10.0-windows    Windows adapters behind the Core seams
  src/StartupProfiles.App    net10.0-windows    WinExe host (WPF UI + tray + loopback API)
    Program.cs     STA entry point (see file for boot sequence)
    Api/           loopback-only ASP.NET Core endpoints (for agents / external tools)
    Tray/          NotifyIcon: re-run a profile, open config, quit
    Launcher/      the minimal login selector (WPF window + view model)
    Config/        the profile editor (WPF window + view models)
    Mvvm/          ObservableObject + RelayCommand (see docs/adr/0001-local-wpf-mvvm.md)
    Themes/        light/dark semantic-token dictionaries + ThemeManager
```

`Core` targets plain `net10.0` so the execution engine and models stay unit-testable and
free of UI concerns. `App` targets `net10.0-windows` and is a **WPF** app: WPF for the launcher
and config windows, a WinForms `NotifyIcon` for the tray, and ASP.NET Core for the loopback API.
The desktop UI talks to Core in-process (through the same stores and `ProfileExecutor`); the
loopback API exists for agents and external tools, not for the built-in UI.

## Data model

```
Profile
  Id                unique id
  Name              display name ("Work")
  Icon              icon reference
  Actions[]         ordered ProfileAction list
  StartupBehaviour  default / remember-last / auto-select-after-timeout
  Conditions[]      optional (network, battery, docked, time-of-day, ...)

ProfileAction
  Type              see action types below
  Target            exe path / url / file / folder / service name / script
  Arguments         optional args
  Delay             delay before running this action
  RunAsAdmin        elevation flag
  FailureBehaviour  continue / stop / retry
```

### Action types (planned)

Launch app - launch with arguments - launch minimized - launch as admin - open URL -
open several URLs - open file - open folder - run PowerShell command/script -
run `.bat`/`.cmd` - start Windows service - start VPN connection - delay an action -
wait until another action finishes - kill/close an application -
check whether an application is already running.

A profile is therefore a small declarative workflow, e.g. *Work*:

```
1. Start VPN
2. Wait until VPN is connected
3. Open Edge
4. Open company portal
5. Start remote-control application
6. Start Teams
```

## Execution engine

`Execution/ProfileRunner` walks a profile's actions in order and:

- honours per-action `Delay` and `wait-for` dependencies,
- runs each action through its `Actions/` handler,
- applies `FailureBehaviour` on error (continue / stop / retry),
- records every attempt to the execution history/log for diagnostics and failed-action
  reporting.

## Storage

Profiles and app config live as plain JSON under `%APPDATA%\StartupProfiles` - no database
engine, nothing leaves the machine. Supports export/import for moving profiles between
computers. (TOML is a possible alternative; JSON is the default to match Treeline.)

## Startup registration

The launcher registers itself to run at login (and, ideally, nothing else does). `Windows/`
owns the registration, process handling, service control, and elevation helpers so `Core`
stays portable-friendly and testable.

## UI surfaces

- **Launcher** - tiny, keyboard-first selector shown at login. Secondary actions: start
  nothing, edit profiles, remember last, auto-pick after N seconds, close without launching.
- **Configuration UI** - the richer profile editor (add/reorder actions, set conditions,
  import/export). Never shown at login unless asked for.
- **Tray** - switch to / re-run another profile after login; open the config UI.

## Implementation status

Milestone 1 implemented `StartupProfiles.Core` (net10.0); Milestone 2 added `StartupProfiles.Windows`
(net10.0-windows) adapters; Milestone 3 built the `StartupProfiles.App` host (loopback API + tray).
Each has an xUnit project under `tests/` wired into the solution.

Project layout now:

```
src/StartupProfiles.Core      net10.0            models, actions, execution, storage (portable seams)
src/StartupProfiles.Windows   net10.0-windows    Windows adapters behind the Core seams
src/StartupProfiles.App       net10.0-windows    WinExe host: loopback API + endpoint.json + tray
tests/StartupProfiles.Core.Tests        net10.0
tests/StartupProfiles.Windows.Tests     net10.0-windows
tests/StartupProfiles.App.Tests         net10.0-windows
```

Platform code stays out of the portable Core: Core owns the seam interfaces
(`Core/Windows/IStartupRegistration`, `IServiceController`, `IVpnConnector`) and the handlers that use
them; `StartupProfiles.Windows` owns the concrete adapters.

Built in Core:

- **Models/** - `Profile`, `ProfileAction`, `ActionType`, `StartupBehaviour`, `FailureBehaviour`,
  and placeholder `ProfileCondition` / `ConditionType` (stored, not evaluated).
- **Actions/** - `IActionHandler` + one handler per type, dispatched through `ActionHandlerRegistry`
  (keyed by `ActionType`; Relay's provider registry is the reference shape). Handlers implemented:
  `LaunchApp`, `OpenUrl` / `OpenFile` / `OpenFolder` (one `ShellOpenHandler` per type), `RunScript`,
  `KillProcess`, `Delay`.
- **Execution/** - `ProfileRunner` walks actions in order, applies each action's lead `Delay`,
  dispatches to the handler, honours `FailureBehaviour` (continue / stop / retry with `RetryCount`),
  and records an `ActionExecution` per action into a `ProfileRun`.
- **Storage/** - JSON persistence under `%APPDATA%\StartupProfiles` (`profiles.json`, `config.json`,
  `history.json`) via `ProfileStore` / `ConfigStore` / `HistoryStore`. A fresh install seeds the six
  default profiles. Enums persist as names; writes are atomic (temp file + move).
- **Windows/** - the platform seam interfaces `IStartupRegistration`, `IServiceController`,
  `IVpnConnector`, plus the `StartServiceHandler` / `StartVpnHandler` action handlers that depend on
  them (in `Actions/`).

Built in StartupProfiles.Windows (adapters):

- `WindowsStartupRegistration` - HKCU `...\CurrentVersion\Run` value (per-user, no elevation).
- `WindowsServiceController` - starts a service via `System.ServiceProcess.ServiceController`, waiting
  for `Running` up to a timeout.
- `WindowsVpnConnector` - connects a named entry via `rasdial` using saved credentials (never passes
  credentials on the command line).
- `WindowsRuntime.CreateActionRegistry()` - the Windows composition root wiring the real launcher plus
  service/VPN adapters into `ActionHandlerRegistry.CreateDefault`.

Built in StartupProfiles.App (host):

- **Program** - STA entry point, single-instance mutex. Builds a loopback-only ASP.NET Core host
  (Kestrel bound to `127.0.0.1`), starts it, writes `endpoint.json`, then runs the tray (or waits
  headless with `--headless`). Composition root: `ProfileStore` / `ConfigStore` / `HistoryStore`,
  `WindowsRuntime.CreateActionRegistry()`, `ProfileRunner`, `ProfileExecutor`, `ConfirmationService`.
- **Api/ApiEndpoints** - the endpoints in [docs/API.md](API.md): health, profile CRUD, run, history,
  and register. Deleting a profile and registering an app are both two-phase (a `ConfirmationService`
  token) so nothing destructive or additive happens without an explicit confirm.
- **Tray** - `NotifyIcon` menu listing profiles (click to re-run via `ProfileExecutor`), open data
  folder, exit.
- `Core/Confirmations/ConfirmationService` and `Core/Execution/ProfileExecutor` (run-then-record) are
  in Core so they are unit-testable; the host and tray both use the executor as the single source of
  truth for running a profile.

Built in StartupProfiles.App (WPF UI - Milestone 4):

- **Launcher** - the login selector (`LauncherWindow` + `LauncherViewModel`): a grid of profile tiles,
  number-key and Escape shortcuts, "Edit profiles" and "Close". Shown at startup; picking a profile
  runs it through `ProfileExecutor` and closes the window (the app stays in the tray).
- **Config** - the profile editor (`ConfigWindow` + `ConfigViewModel`, `ProfileEditor`, `ActionEditor`):
  list/create/delete profiles, edit name/icon/startup behaviour, add/remove/reorder typed actions,
  save, run, and JSON export/import. Deleting asks for confirmation; dialogs are behind the
  `IUserPrompts` seam so the view models are unit-tested without a UI.
- **Mvvm** - local `ObservableObject` / `RelayCommand` mirroring dotnetlib's shape.
- **Themes** - light/dark semantic-token `ResourceDictionary` files and a `ThemeManager` that follows
  the Windows setting in `System` mode; the config window exposes a System/Light/Dark picker.
- The WPF UI calls Core directly (no HTTP); a WinForms `NotifyIcon` provides the tray on the WPF
  message loop.

Deviations and decisions worth noting:

- `ProfileAction` gained a `RetryCount` field (default 1) to bound `FailureBehaviour.Retry`; it is
  not in the original field list above.
- I/O is behind injected seams so the runner is deterministic in tests (CodePrint rule: isolate the
  clock, delays, and process launching): `IProcessLauncher` / `SystemProcessLauncher`, `IDelayer` /
  `TaskDelayer`, and `TimeProvider`. Composition happens in `ActionHandlerRegistry.CreateDefault`;
  the App host will own the real composition root.
- `ActionType.StartService` / `StartVpn` handlers are registered only when their platform adapter is
  passed to `ActionHandlerRegistry.CreateDefault`. A portable host (or a unit test) that omits them
  records a clear "no handler" failure rather than throwing; the Windows host wires them via
  `WindowsRuntime`.
- `IStartupRegistration` is a host service (not an action). The Windows adapter targets a per-user Run
  key; its registry path is injectable so tests use a throwaway HKCU subkey instead of the real key.
- Launch-minimized and launch-as-admin are flags on the launch action (`RunAsAdmin`), not separate
  action types; "open several URLs" is several `OpenUrl` actions; `wait-for` and `check-running` are
  deferred.
- A root `Directory.Build.props` (mirroring `dotnetlib`) enables nullable, implicit usings,
  `TreatWarningsAsErrors`, and `latest-recommended` analysis; `tests/Directory.Build.props`
  suppresses CA1707 for `Method_Condition_ExpectedResult` test names.
- The UI is **WPF**, not the WebView2 approach the original design named. The reasons and trade-offs
  are in [docs/adr/0001-local-wpf-mvvm.md](adr/0001-local-wpf-mvvm.md).
- The App project pins `RuntimeFrameworkVersion` to `10.0.7` because the dev SDK resolves `10.0.9` for
  the WPF markup-compile helper, which is not installed here; roll-forward still runs it on newer
  patches. Revisit once the box has a matching runtime.

Built in Core/Integration and StartupProfiles.App/Integration (Milestone 5):

- **Core/Integration** - `RegistrationRequest`, `RegistrationRequestParser` (one field mapping shared
  by the protocol URI and the CLI form), and `IProfileRegistrar` / `ProfileRegistrar` (appends a launch
  action to the chosen profiles, idempotent per target, never creating profiles or touching unlisted
  ones). `RegistrationOutcome` reports what changed.
- **App/Integration** - `RegistrationLaunch` recognises and parses a `register` invocation;
  `RegistrationApp` runs the one-shot flow (no mutex, no API host, no tray) and shows the trusted
  `RegistrationWindow` / `RegistrationViewModel`; a `suggestedProfile` only pre-ticks a hint and never
  `Everything`. `RunningInstance` + `ApiProfileRegistrar` route the write through a live host's loopback
  API so the running app stays the single writer of `profiles.json`; a direct store write is used only
  when no instance is running.
- **Api** - `POST /api/register` applies a request via `IProfileRegistrar` behind the same two-phase
  confirmation token as delete.

Still open on the contract: registering the `startupprofiles://` scheme with Windows (installer
milestone), and `supportsMinimized` is carried as metadata only (launch-minimized is not yet an action
field). The Windows adapters carry platform tests only for the registry round-trip; service and VPN
control are thin wrappers over the OS and are exercised manually. WPF windows are covered by view-model
tests, not UI automation.

## Future ideas (not scheduled)

Keyboard shortcuts per profile - Start-menu launch - profile chaining - scheduled profiles -
network / dock / battery aware suggestions - shut down apps when switching - per-profile
env vars / power plan / audio device / display config - sync between machines.
