# Handoff

Snapshot of Windows Startup Profiles as of 2026-08-31. For the full design see
[docs/ARCHITECTURE.md](ARCHITECTURE.md); for conventions see [AGENTS.md](AGENTS.md) and
[CONTRIBUTING.md](CONTRIBUTING.md).

## Status: Milestones 1-5, installer, release CI, UI polish, startup takeover, base done; conditions next

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

- Build is clean (0 warnings, warnings-as-errors on) and 191 tests pass.
- **Run `install.ps1` / `uninstall.ps1` (and anything that reads or writes the app's data or HKCU) from a
  normal terminal, not from a process started inside a packaged (MSIX) app such as the Claude desktop app.**
  Windows redirects AppData and some HKCU writes from packaged processes into a private per-package copy
  (`%LOCALAPPDATA%\Packages\<pfn>\LocalCache\...`), so the install silently lands in the wrong place and
  that process sees a different data folder than the real app. Launching through `explorer.exe` breaks out
  of the package context; the app's loopback API is unaffected and safe to use from anywhere.
- Run the app: `dotnet run --project src/StartupProfiles.App` (add `--headless` for the API only,
  `--port N` to override the port, default 8790).
- Data lives in `%APPDATA%\StartupProfiles` (`profiles.json`, `config.json`, `history.json`,
  `endpoint.json`, `startup-takeover.json`, `startup-seen.json`, `base.json`, `library.json`). Nothing leaves the machine.

## What exists

| Project | TFM | Contents |
| --- | --- | --- |
| `StartupProfiles.Core` | net10.0 | Models, action handlers + registry, `ProfileRunner`/`ProfileExecutor`, JSON stores, `ConfirmationService`, and the Windows seam interfaces. Portable and unit-tested. |
| `StartupProfiles.Windows` | net10.0-windows | Windows adapters: startup registration (HKCU Run key), service control, VPN (`rasdial`), and the action-registry composition root. |
| `StartupProfiles.App` | net10.0-windows | WPF launcher + config UI, the tray (DotNetLib.Tray), the updater, and the loopback ASP.NET Core API + `endpoint.json`. |
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
  [docs/adr/0001-local-wpf-mvvm.md](docs/adr/0001-local-wpf-mvvm.md). MVVM, the tray, the theme
  engine, single instance, and self-update now come from `DotNetLib.Core` / `DotNetLib.Tray` 0.2.0 on
  GitHub Packages ([ADR 0003](docs/adr/0003-adopt-dotnetlib-packages.md)); restoring needs the
  `dotnetlib` NuGet source with a `read:packages` token (README, Requirements; CI uses the
  `DOTNETLIB_PACKAGES_TOKEN` secret).
- The App pins `RuntimeFrameworkVersion=10.0.7` to work around a dev-box SDK/runtime mismatch (the WPF
  markup-compile helper wanted 10.0.9). Remove the pin when the box has a matching runtime.
- Conventions come from the owner's `CodePrint` (rules) and `dotnetlib` (.NET architecture) repos, with
  `Relay`/`MicForge` as reference implementations. One top-level type per file; Conventional Commits;
  no AI-attribution trailer; PolyForm Noncommercial license.

## Milestone 5 - integration contract (done)

`Core/Integration` (portable) + `App/Integration` (host) implement the contract from
[docs/INTEGRATION.md](docs/INTEGRATION.md). `RegistrationRequestParser` parses both the
`startupprofiles://register?...` URI and the `register --app-id ...` CLI form; `RegistrationTargets` is
where the user chose to put the app, and `ProfileRegistrar` keeps it in the global library and appends a
linked launch action to each chosen destination (idempotent per target). Three entry points feed one
trusted, Startup Profiles-owned confirmation window: the protocol, the CLI, and `POST /api/register`
(two-phase confirm, like delete). A one-shot `register` invocation routes its write through a running
instance's loopback API when one is live, so the running app stays the single writer of `profiles.json`.

The window offers three answers: **Add** to the ticked destinations, **Just recognize** (library only, to
drag into a profile later), or **Don't add**. Destinations are the **Base** pinned first, then every
profile, each with its glyph and what it already starts, in a scrolling list; the window is resizable and
capped to the screen's work area. A profile that includes the base is reported as "already in" when the
base starts the app, so it never launches twice. `POST /api/register` mirrors all of it: `includeBase`
plus `profileIds`, and an empty pair is the recognize-only case.

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
`%APPDATA%\StartupProfiles`). Switches: `-Port`, `-FrameworkDependent`, `-NoStartup`,
`-KeepStartupApps`, `-NoProtocol`, `-NoSkill`.

## Startup takeover (done)

The app reads what Windows starts at login through `IStartupAppCatalog` /
`WindowsStartupAppCatalog`: the per-user and all-users `Run` keys (incl. WOW6432Node), both Startup
folders, and packaged apps' startup tasks (`SystemAppData\<family>\<task>` `State`, app id resolved
from the package's AppxManifest.xml). On/off state comes from the `Explorer\StartupApproved` flags
(even first byte = on, odd = off) and the task `State` (2 on, 1 off). `StartupCommandLine` turns a
`Run` value into a `LaunchApp` action (quoted or unquoted-with-spaces paths); Startup folder items
become `OpenFile` on the shortcut; packaged tasks become `explorer.exe shell:AppsFolder\<family>!<app>`.

`StartupTakeover` (Core) is the install default: every enabled, per-user-switchable, launchable entry
(except the launcher's own `Run` value) is appended to the Everything profile (deduped, recreated if
deleted), recorded in `startup-takeover.json`, then switched off. All-users and policy-locked entries
need admin and are left on. `Restore` switches the recorded entries back on. Maintenance commands:
`--list-startup` (read-only report), `--take-over-startup` (install.ps1, unless `-KeepStartupApps` /
`-NoStartup`), `--restore-startup` (uninstall.ps1). `--register-login` now also clears a Task Manager
"disabled" flag on the launcher's own entry.

Known wrinkles: packaged apps launched from a profile open normally rather than via their minimized
startup activation, and an app registered both ways (e.g. Teams: a `Run` value and a packaged task)
gets two launch actions.

## Releases

`.github/workflows/release.yml` cuts a release on a `v*` tag push: it derives the version from the tag,
stamps it into the assembly via `-p:Version=<tag>` (the one version source is `VersionPrefix` in
`Directory.Build.props`; Debug builds add a `-dev` suffix), tests, publishes a self-contained win-x64
build, zips it as `StartupProfiles-<version>-win-x64.zip`, and attaches it to a GitHub Release with
generated notes. It also builds the Inno installer from the signed publish folder, signs it, and attaches
`StartupProfiles-Setup-<version>.exe` with its `.sha256` (checksums are written after signing). To ship:
bump `VersionPrefix`, then push the matching tag `vX.Y.Z`.

**The updater needs public releases**: it reads `releases/latest` without a token, and the repository is
private today. Until it is public (or releases move to a public repo), "Check for updates" says it could
not reach the release page.

## UI / visual polish (done)

`Themes/Controls.xaml` holds a shared implicit-style dictionary (buttons, text boxes, combo boxes,
list boxes, checkboxes, DataGrid, tabs, thin scrollbars) that both themes merge and re-color live via
`DynamicResource`. Native window title bars follow the theme (DWM immersive dark mode); the tray menu is
WPF UI's, themed by DotNetLib's theme engine. The app has an icon (`Assets/app.ico`) on the exe, title
bars, and tray. The config window is split into **Profiles** and **Settings** tabs (theme picker,
export/import, a disabled "Sync - coming soon" placeholder, and version). The launcher hides profile
labels below a width threshold (icons-only) instead of clipping text. Profiles have **emoji icons**
chosen from a picker (`IUserPrompts.PickIcon`), rendered on the launcher tiles. A global dispatcher
exception handler logs to `error.log` and keeps the app alive.

## Base (done)

The base is an action list that runs before whichever profile is run - from the launcher, tray, config
window, or API - unless that profile unticks **Include base** (`Profile.IncludeBase`, default true).
It is deliberately not a profile: `StartupBase` in `base.json` via `IBaseStore`, no launcher tile.
`ProfileExecutor` prepends the base actions into the same run. The config window pins **Base** above
the profiles (same action grid; no name/icon/startup/delete) and its "Run now" runs the base alone. API:
`GET`/`PUT /api/base`, `POST /api/base/run`; profile summaries carry `includeBase`.

Switching profiles from the tray reruns the base actions (by design - the owner chose "with every profile
run"). Backups include the base (see ARCHITECTURE.md, Data safety).

## Relaunch and action icons (done)

DotNetLib's `SingleInstance` (one per Windows user, a named pipe): launching the app while it already
runs (Start Menu, shortcut) knocks on the running instance, which shows its launcher (or brings the open
one forward) instead of the second process exiting silently. The launcher is a single window.

The config window's action tables show each action's shell icon, like Windows' Startup apps page:
`ActionIconSource` maps an action to a shell item (exe/shortcut/folder path, `shell:AppsFolder\...` for
packaged apps, the Squirrel `Update.exe --processStart X.exe` app, `.url` for URLs; none for delays,
kills, services, inline commands) and `ShellIcons` loads it via `SHParseDisplayName` + `SHGetFileInfo`,
cached per source. Known gap: app execution aliases (e.g. Teams' `WindowsApps\...\ms-teams.exe` Run
value) show a generic icon.

## Side panel, global library, and drag and drop (done)

The config window's Profiles tab has a right-hand panel with two tabs:

- **Defaults** - every launchable `IStartupAppCatalog` entry (on or off, except Startup Profiles itself),
  sorted by name, with its shell icon and "Registry / Startup folder / Store app · starts with Windows /
  off in Windows". Catalog names use the exe's file description, as Task Manager does ("Microsoft Edge"
  instead of `MicrosoftEdgeAutoLaunch_...`), except Squirrel's shared `Update.exe`.
- **Created** - the global library (`LibraryItem` in `library.json` via `ILibraryStore`; operations in
  `Core/Library/LibraryService`): startable items set up once, with a form to add (+ New), edit, and
  delete them (`App/Config/LibraryPanel`).

Profile and base actions **link** to library items (`ProfileAction.LibraryItemId`, owner's choice over
copying): `ProfileExecutor` resolves the link at run time (the item supplies type/target/arguments/admin;
delay/failure/retries stay per row), and the stored values are a last-saved copy used if the item is gone.
Deleting an item (`LibraryService.Remove`, confirmed with the list of profiles using it) rewrites its links
as standalone copies. In the table a linked row shows the item's name and an accent link badge, and its
Type/Target/Arguments/Admin cells refuse edits (edit the item in Created instead); saving an item relinks
open rows.

Drag and drop (view plumbing in `ConfigWindow.xaml.cs`, behaviour in `ConfigViewModel`):

- Defaults item -> table: kept in the library (`LibraryService.FindOrAdd` reuses an item that starts the
  same thing) and added as a linked row. Created item -> table: linked row. Double-click does the same.
- Row grip -> table: reorder (`MoveAction`, lands above the drop row). Row grip -> Created: saved to the
  library and the row linked (`SaveActionToLibrary`; name from `Startables.NameFor`).
- Defaults item or Explorer files/folders -> Created: saved. Explorer files -> table: saved and linked
  (`.exe` -> LaunchApp, folder -> OpenFolder, else OpenFile).
- Dropping on a row inserts above it, elsewhere appends; an item already in the table is selected (and
  linked) instead of added twice.
- Anything dropped onto a sidebar profile (or Base) is added to it without opening it
  (`ConfigViewModel.DropOnProfile`): panel items, Explorer files, or a copied table row.
- Every change saves by itself (see the profile editor section below). Library changes persist immediately.
- While a profile that includes the base is edited, the panel hides anything the base already starts
  (`IsOfferedInPanel`, applied as a `CollectionView` filter refreshed on `PanelFilterChanged`), and adding
  such an item to that profile is refused.

Not yet: the install-time startup takeover still adds standalone copies to Everything (drag those rows
into Created to link them). Backups include the library. The drag gestures
are verified by view-model tests and offscreen rendering, not UI automation.

## Profile editor: cards and auto-save (done)

The actions table is now a list of cards (`ConfigWindow.xaml`, `App.ActionCard` style). Each card shows the
step's icon (shell icon, or a Fluent glyph per type from `ActionLabels.Glyph` when the shell has none), a
plain name (`ActionEditor.DisplayName`), a second line like "App · C:\...\Code.exe" (Store apps say "Microsoft
Store app"), and chips for non-default options ("waits 5 s", "as admin", "retries 3x"). Clicking a card opens
its details inline with plain labels ("Kind", "Wait first", "If it fails"); enum values show through
`ActionLabels` / `Choice<T>` and are stored unchanged. App, file, and folder targets have a Browse button
(`IUserPrompts.PickTarget`). The card header is the drag handle.

Auto-save: `ProfileEditor.Changed` fires on any edit (name, icon, toggles, any row value, rows added, removed, or
moved). `ConfigViewModel` hands the write to an `ISaveScheduler`: the window uses `DebouncedSaveScheduler`
(500 ms after the last edit), tests use the immediate default. A waiting save is flushed when another profile
opens, on import, and when the window closes, and dropped when the profile is deleted. A blank name is not
saved; the footer says why. The footer shows "All changes saved" and the last action. There is no Save button.

Removing: a trash icon appears on the hovered or selected card (red on hover); the Delete key does the same.
Removal saves at once and the footer offers **Undo**, which puts the card back in place (for the profile it came
from only).

Icons: the picker groups icons (Work, Code, Study, Play, Life, Symbols; `Ui/IconCatalog`), rings the current
one, and draws them in the text color (the old picker drew black glyphs on the dark theme). The sidebar shows each
profile's icon and "N steps"; the editor header shows a large icon button with an edit badge.
## Launcher refresh and At login (done)

The launcher greets by time of day ("Good morning"), shows tiles with each profile's icon, step count, and 1-9
shortcut, and tags the profile that ran last ("last time", read from `history.json`, base runs ignored). That
tile is pre-selected: Enter starts it, arrow keys move. "Close" is now "Not now".

`Profile.StartupBehaviour` is now used. The editor calls it **At login**: "Ask me" (`Default`), "Start it if I
used it last" (`RememberLast`), "Always start it" (`AutoSelectAfterTimeout`). Only the launcher opened at
login (`LauncherViewModel` with `atLogin: true`) acts on it: the last-used profile wins if it may start by
itself, else the first "Always start it" one. It counts down `CountdownSeconds` (10) in a banner with Cancel;
any click or key cancels. The window drives `Tick()` from a one-second `DispatcherTimer`, so tests step the
countdown directly. A launcher reopened from the tray or Start Menu never counts down.
## Startup discovery (done)

Apps keep adding themselves to Windows startup after install. At launch (UI mode, after the login launcher
opens) `StartupDiscovery.FindNew` runs off the UI thread and reports enabled, per-user-switchable, launchable
entries that were not there last time (`startup-seen.json`; the very first look only records, so existing
installs are not flooded). They are shown together in **New startup apps** (`App/Discovery`) once the launcher
closes. Each row picks "Start with <profile>", "Start with Base", "Keep in Created for later", or "Leave it to
Windows". The default is Everything when startup apps were taken over before (`StartupTakeover.HasTakenOver`),
else leave it. Apply places each one; a profile or Base choice adds a linked step and switches the entry off in
Windows, recorded in `startup-takeover.json` so uninstall restores it. "Ask me later" decides nothing, so they
come back next launch. Settings has a checkbox to turn the check off (`discoverStartupApps` in `config.json`).
## DotNetLib tray and themes, installer, and updates (done)

See [ADR 0003](docs/adr/0003-adopt-dotnetlib-packages.md) and ARCHITECTURE.md (UI surfaces). The tray
is `TrayIconHost` + `TrayMenuBuilder` with a tested `TrayMenuModel`; the theme is `TrayThemeApplier` with
the app's tokens and styles layered on top (plus a high-contrast token set); WinForms is gone. WPF UI's
implicit styles now reach controls the app does not style; the combo box's inner toggle opts out (see
docs/pitfalls.md).

`installer/build-installer.ps1` builds `installer/dist/StartupProfiles-Setup-<version>.exe` locally (needs
Inno Setup 6). `UpdateCoordinator` checks once a day and on demand, verifies the installer's SHA-256, and
installs silently on consent; Settings > Updates and the tray show it.


1. **Profile conditions**: `ProfileCondition`/`ConditionType` are stored but not evaluated.
2. Make releases public (or publish them to a public repo) so the updater can see them, then cut the
   first release.
3. Optional: a real "Sync between machines" implementation (the Settings placeholder is still there).
