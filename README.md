<div align="center">
  <h1>Windows Startup Profiles</h1>
  <p>Stop launching <em>everything</em> at login. Launch what fits what you're about to do.</p>
</div>

Windows startup is essentially binary: an app either **always** starts or **never** starts.
But most startup apps are *contextually* useful, not universally useful - a VPN and Teams
belong to work mornings, Steam and Discord belong to game nights, an IDE and a terminal
belong to a dev session.

**Windows Startup Profiles** adds the missing middle layer: **start when relevant.**

Only this launcher runs at login. Right after you sign in it shows a small, fast, modern
selector:

```
What are you doing?

[ Work ]   [ Dev ]   [ School ]
[ Games ]  [ Chill ] [ Everything ]
```

Pick a context and it runs only the apps, URLs, scripts, folders and services that belong
to that profile - as a small declarative startup workflow, not just a list of `.exe`s.

> Status: **early**. The portable Core (models, storage, execution engine) and the Windows platform
> adapters (startup registration, service, VPN) are implemented and tested; the App host, launcher UI,
> loopback API, and integration contract are not built yet. See
> [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the plan and the current implementation status.

## The idea

- **One thing auto-starts.** The launcher is one of the very few apps allowed to run at
  login. It replaces the pile of individual startup entries.
- **You choose the context.** The launcher is deliberately small and fast - a polished
  system dialog, not a settings app.
- **Profiles are data, not code.** A profile is a list of typed actions
  (launch app, open URL, run script, start service, start VPN, delay, wait-for, kill, ...).
- **Rich editing lives elsewhere.** Profile configuration happens in a separate management
  UI. The startup selector stays minimal.

### Example profiles

| Profile | Typical actions |
| --- | --- |
| **Work** | Start VPN -> wait for connection -> open Edge + company portal -> remote-control app -> Teams |
| **Dev** | IDE / VS Code, terminal, Git tools, local services, project folders, AI assistants |
| **School** | Browser w/ university pages, notes, Teams, PDF tools, relevant folders, school VPN |
| **Games** | Steam, Discord, peripheral / RGB / monitoring utilities (no dev/work apps) |
| **Chill** | Browser, Spotify, Discord, everyday apps |
| **Everything** | The traditional "start all startup apps" behavior |

### Secondary launcher actions

Start nothing - edit profiles - remember last profile - auto-pick after N seconds - close
without launching.

## Architecture

A portable `net10.0` core drives everything; a `net10.0-windows` WPF host provides the UI, a tray
icon, and a loopback API. Profiles persist as JSON under `%APPDATA%`, and a Claude Code skill is
bundled.

```
StartupProfiles.slnx
  src/StartupProfiles.Core     (net10.0)          profiles, actions, execution engine, storage
  src/StartupProfiles.Windows  (net10.0-windows)  Windows adapters (startup, service, VPN)
  src/StartupProfiles.App      (net10.0-windows)  WPF launcher + config UI, tray, loopback API
```

| Layer | Responsibility |
| --- | --- |
| **Core / Models** | `Profile`, `ProfileAction`, action types, conditions |
| **Core / Actions** | One handler per action type |
| **Core / Execution** | Ordered execution engine: delays, wait-for, failure behavior, logging |
| **Core / Storage** | Profile + config persistence (JSON under `%APPDATA%\StartupProfiles`) |
| **Core / Windows** | Startup registration, process handling, services, elevation |
| **Core / Integration** | Public registration contract for other apps (see below) |
| **App / Launcher** | The minimal login selector |
| **App / Api + Tray** | Loopback config API and the switch-profile tray icon |

Full write-up: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Integration contract

Other applications can *ask* to be added to a profile - they never add themselves. The
requesting app supplies metadata (id, name, launch target, icon, suggested profile); Startup
Profiles owns the confirmation UI and all configuration authority. Transport is a Windows
custom protocol (`startupprofiles://register?...`) and/or a CLI (`StartupProfiles.exe register ...`).

For CodePrint-generated Windows apps this shows up as an optional
**Settings -> Startup -> Add to Startup Profiles** action, and the app must work normally
when Startup Profiles is not installed.

Contract details: [docs/INTEGRATION.md](docs/INTEGRATION.md).

## Requirements

- Windows 10/11
- .NET 10 SDK (to build)

## Quick start (planned)

```powershell
git clone https://github.com/lukr-99/startup-profiles.git
cd startup-profiles
dotnet build StartupProfiles.slnx
dotnet test StartupProfiles.slnx
```

An `install/install.ps1` script (register at login, Start Menu shortcut, install the agent
skill) will land with the first working build.

## License

[PolyForm Noncommercial 1.0.0](LICENSE.md) - free for noncommercial use. Copyright 2026 lukr-99.
