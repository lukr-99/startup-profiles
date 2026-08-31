# Startup Profiles integration contract

> Implemented in Milestone 5. Both transports (protocol URI and CLI), the loopback API path, and
> the trusted confirmation window exist; see [Implementation](#implementation). Registering the
> `startupprofiles://` scheme with Windows is the installer's job (a later milestone), so today the
> protocol form is exercised by passing the URI to `StartupProfiles.exe` directly.

Startup Profiles exposes a small **public integration contract** so other applications can
*request* to be added to a startup profile. This is aimed at CodePrint-convention Windows
apps but is not specific to them.

The core distinction:

> The external application **requests** registration. Startup Profiles **owns** the decision
> and the configuration UI.

An application effectively says *"I support Startup Profiles - would you like to add me?"*
It never silently adds itself.

## Registration flow

1. User installs or first launches an application.
2. The application detects that Startup Profiles is installed.
3. It invokes the Startup Profiles registration contract (protocol or CLI).
4. Startup Profiles opens **its own trusted** registration window.
5. The window shows the requesting application's information.
6. The user chooses where it belongs: Work / Dev / School / Games / Chill / Everything /
   multiple profiles / **Don't add**.
7. Startup Profiles stores the configuration.
8. The requesting application receives a success / cancelled result if appropriate.

## Transport (proposed)

Prefer a Windows-native custom protocol:

```
startupprofiles://register?appId=...&name=...&target=...&icon=...&args=...&publisher=...&suggestedProfile=...
```

and/or a small CLI:

```
StartupProfiles.exe register --app-id ... --name ... --target ... [--icon ...] [--args ...] \
                             [--publisher ...] [--suggested-profile Dev] [--supports-minimized]
```

The exact transport can change; the conceptual contract below should stay stable.

## Registration metadata

| Field | Meaning |
| --- | --- |
| Application ID | stable unique id for the requesting app |
| Display name | shown in the confirmation UI |
| Executable path / launch target | what a launch action would run |
| Icon | shown in the confirmation UI |
| Default launch arguments | optional args |
| Publisher | shown for trust |
| Suggested profile / category | a *hint* only; the user decides |
| Supports minimized startup | capability flag |
| Optional integration capabilities | see "Future contract expansion" |

## Security / UX rules

External applications must **not** be able to:

- add themselves silently,
- modify existing profiles without consent,
- remove other applications,
- automatically assign themselves to `Everything`,
- execute arbitrary Startup Profiles configuration changes.

Every registration results in a **Startup Profiles-owned confirmation UI**. The requesting
app supplies metadata and a request - not configuration authority. A suggested profile is a
hint; the user always chooses, and `Everything` is never auto-selected.

## CodePrint convention

When designing a CodePrint Windows application, consider Startup Profiles integration:

- If Startup Profiles is installed, the app may expose **Add to Startup Profiles**, e.g.
  under **Settings -> Startup -> Add to Startup Profiles**, or offered once during setup.
- The app must work completely normally when Startup Profiles is **not** installed.
- Integration is therefore an **optional capability, never a hard dependency**.

## Implementation

The contract is realised in two layers:

- **`StartupProfiles.Core/Integration`** (portable): `RegistrationRequest` is the metadata an app
  supplies; `RegistrationRequestParser` parses both transports through one shared field mapping and
  validation; `ProfileRegistrar` applies an approved request by appending a launch action to the
  chosen profiles (idempotent per target, never creating profiles or touching unlisted ones).
- **`StartupProfiles.App/Integration`** (host): the entry points and the trusted window.

Three ways to reach it:

1. **Protocol** - `StartupProfiles.exe "startupprofiles://register?appId=...&name=...&target=..."`.
   Registering the scheme with Windows is deferred to the installer; until then, invoke the exe with
   the URI directly.
2. **CLI** - `StartupProfiles.exe register --app-id ... --name ... --target ... [--args ...]
   [--publisher ...] [--suggested-profile Dev] [--supports-minimized]`. A value that itself starts
   with `--` (launch arguments) can use the `--args=--flag` form.
3. **Loopback API** - `POST /api/register` for local agents (see [API.md](API.md)).

The protocol and CLI forms open a **Startup Profiles-owned** confirmation window: it shows the
requesting app's name, publisher, and target, and a checklist of profiles. A `suggestedProfile` only
pre-ticks that one profile as a hint - never `Everything` - and the user must click **Add**. Because a
running instance caches profiles in memory, a one-shot `register` process routes its write through the
running instance's loopback API when one is live (keeping a single writer of `profiles.json`), and
writes directly only when no instance is running.

## Future contract expansion

The same contract could later let an app declare richer capabilities than a single launch,
and let the user pick which capability belongs to a profile:

```
MyApp
  Default          normal startup
  Open Project     specific args / deep link
  Background Agent  background-only mode
  Start Minimized  minimized startup
```

Possible declared capabilities: normal startup action, minimized startup action, specific
startup arguments, deep links, available modes, background-only mode, dependencies, and
before/after-launch hooks.
