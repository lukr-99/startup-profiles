# Windows Startup Profiles

Startup Profiles replaces the pile of Windows startup apps with one launcher. At login the user picks
what they are about to do, and only the apps and steps for that context run.

## Language

**Profile**:
A named context, such as Work or Games, that holds an ordered list of actions. It is data, not code.
_Avoid_: Preset, mode, workflow

**Action**:
One typed step in a profile or the base, such as launching an app, opening a URL, starting a service,
or waiting. Each row in the configuration window's table is one action.
_Avoid_: Task, item, entry

**Base**:
The list of actions that runs before whichever profile is run, unless that profile turns off
**Include base**. The base is not a profile and has no launcher tile.
_Avoid_: Default profile, common profile, always-on profile

**Library item**:
A startable thing (app, file, folder, URL) set up once in the global library and shown in the
Created tab. It holds the type, target, arguments, and admin flag.
_Avoid_: Template, shortcut, saved action

**Linked row**:
An action that points at a library item. The item supplies what to start at run time; delay, failure
handling, and retries stay on the row.
_Avoid_: Reference row, copy

**Startup takeover**:
The install step that moves the apps Windows starts at login into the Everything profile and
switches them off in Windows, so only the launcher starts with Windows. Restore switches them back on.
_Avoid_: Import, migration, startup sync

**Startup discovery**:
The check at launch for apps that set themselves to start with Windows since last time. Each one is
offered once, and the user picks the profile (or Base) it should start with instead.
_Avoid_: Startup scan, auto-import

**Defaults panel**:
The side panel tab that lists what Windows itself starts at login, on or off, so the user can drag
those apps into a profile or the library.
_Avoid_: Startup list, system apps

**Registration request**:
Another app asking to be added to Startup Profiles, through a `startupprofiles://` link, the CLI, or
the API. The user decides in a Startup Profiles window; an app never adds itself.
_Avoid_: Self-registration, install hook

**Launcher**:
The small window shown at login that asks which profile to run. Rich editing lives in the
configuration window, not here.
_Avoid_: Picker, selector dialog, dashboard
