# Architecture

> Design document. Milestones 1-2 are implemented: the Core domain, storage, and execution engine,
> plus the Windows platform adapters (startup registration, service, VPN). The App host, UI, loopback
> API, and integration contract are not. See [Implementation status](#implementation-status) for
> exactly what exists today.

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
  src/StartupProfiles.App    net10.0-windows    WinExe host
    Program.cs     STA entry point (see file for boot sequence)
    Api/           loopback-only ASP.NET Core endpoints for the config UI
    Tray/          NotifyIcon: re-run / switch profile, open config, quit
    Launcher/      the minimal login selector window (WebView2 over the loopback server)
    wwwroot/       launcher + config UI assets
```

`Core` targets plain `net10.0` so the execution engine and models stay unit-testable and
free of UI concerns. `App` targets `net10.0-windows` (WinForms + ASP.NET Core, WebView2 UI),
mirroring the Treeline host pattern.

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

Milestone 1 implemented `StartupProfiles.Core` (net10.0, no UI/ASP.NET deps); Milestone 2 added
`StartupProfiles.Windows` (net10.0-windows) with the platform adapters. Both have xUnit projects under
`tests/` wired into the solution. `App` is still the skeleton host.

Project layout now:

```
src/StartupProfiles.Core      net10.0            models, actions, execution, storage (portable seams)
src/StartupProfiles.Windows   net10.0-windows    Windows adapters behind the Core seams
src/StartupProfiles.App       net10.0-windows    skeleton host (references Core + Windows)
tests/StartupProfiles.Core.Tests        net10.0
tests/StartupProfiles.Windows.Tests     net10.0-windows
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

Not yet started: `Core/Integration`, and everything under `App` (loopback API, tray, launcher, config
UI). The Windows adapters carry platform tests only for the registry round-trip; service and VPN
control are thin wrappers over the OS and are exercised manually. The PolyForm/AGENTS baseline
documents remain open items tracked outside these milestones.

## Future ideas (not scheduled)

Keyboard shortcuts per profile - Start-menu launch - profile chaining - scheduled profiles -
network / dock / battery aware suggestions - shut down apps when switching - per-profile
env vars / power plan / audio device / display config - sync between machines.
