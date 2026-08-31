# Architecture

> Design document for the skeleton. Nothing here is implemented yet; this is the blueprint
> the empty projects are laid out against.

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

## Future ideas (not scheduled)

Keyboard shortcuts per profile - Start-menu launch - profile chaining - scheduled profiles -
network / dock / battery aware suggestions - shut down apps when switching - per-profile
env vars / power plan / audio device / display config - sync between machines.
