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

> Status: **working**. The portable Core, the Windows adapters, the App host (loopback API + tray), the
> WPF launcher and config UIs, and the integration contract (protocol / CLI / API + confirmation window)
> are implemented and tested. A per-user installer (`StartupProfiles-Setup-<version>.exe`) installs it and
> the app updates itself from GitHub Releases. Still to come: profile conditions and sync between
> machines. See [ARCHITECTURE.md](ARCHITECTURE.md) for the current implementation status.

## The idea

- **One thing auto-starts.** The launcher is one of the very few apps allowed to run at
  login. It replaces the pile of individual startup entries.
- **You choose the context.** The launcher is deliberately small and fast - a polished
  system dialog, not a settings app.
- **Profiles are data, not code.** A profile is a list of typed actions
  (launch app, open URL, run script, start service, start VPN, delay, wait-for, kill, ...).
- **A base for the always-on stuff.** The base is a list of actions (not a profile) that runs before
  whichever profile you pick - noise suppression, cloud sync, peripheral tools. A profile can opt out
  with its **Include base** checkbox.
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

Full write-up: [ARCHITECTURE.md](ARCHITECTURE.md).

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
- To build: read access to the owner's DotNetLib packages on GitHub Packages. Once, run
  `gh auth refresh -s read:packages`, then
  `dotnet nuget add source https://nuget.pkg.github.com/lukr-99/index.json --name dotnetlib --username lukr-99 --password (gh auth token)`
  (the name must be `dotnetlib`, to match `nuget.config`)

## Quick start

Build and run from source:

```powershell
git clone https://github.com/lukr-99/startup-profiles.git
cd startup-profiles
dotnet build StartupProfiles.slnx
dotnet test StartupProfiles.slnx
dotnet run --project src/StartupProfiles.App
```

Or install it for the current user (no elevation) with the installer from the
[latest release](https://github.com/lukr-99/startup-profiles/releases/latest), or build one with
`.\installer\build-installer.ps1` (needs Inno Setup 6). It installs to
`%LOCALAPPDATA%\Programs\StartupProfiles`, starts at login, registers the `startupprofiles://` protocol,
installs the Claude agent skill, and on a first install offers to take over Windows startup (below).
Uninstall it from Windows Settings > Apps; that switches the taken-over startup apps back on and keeps
your profiles in `%APPDATA%\StartupProfiles`. The app checks once a day for a new version and installs
it when you agree (Settings > Updates, or the tray menu).

From source, the script installer does the same. This publishes the app to
`%LOCALAPPDATA%\Programs\StartupProfiles`, adds a Start Menu shortcut, registers the launcher to run
at login, registers the `startupprofiles://` protocol, and installs the Claude agent skill:

```powershell
.\install\install.ps1
```

Install also **takes over Windows startup**: every startup app that is currently on (per-user `Run`
key, your Startup folder, and packaged Store-app startup tasks) is added to the **Everything** profile
and switched off in Windows - the same reversible switch Task Manager uses - so Startup Profiles is the
one app that starts at login. All-users entries need admin, so they are left on. To see what it would
pick up, run `StartupProfiles.exe --list-startup`.

Useful switches: `-FrameworkDependent` (smaller build, needs the .NET 10 Desktop Runtime),
`-KeepStartupApps` (leave existing startup apps alone), `-NoStartup` (don't run at login; implies
`-KeepStartupApps`), `-NoProtocol`, `-NoSkill`, `-Port <n>`. Remove everything with
`.\install\uninstall.ps1`, which switches the taken-over startup apps back on (add `-PurgeData` to
also delete your profiles).

## License

[PolyForm Noncommercial 1.0.0](LICENSE.md) - free for noncommercial use. Copyright 2026 lukr-99.
