# 0001 - WPF UI with local MVVM/theme instead of WebView2 or a dotnetlib dependency

Status: accepted (2026-08-31)

## Context

The original design (README, ARCHITECTURE) called for the launcher and config UIs to be a WebView2
window rendering HTML/JS served from `wwwroot` over the loopback API, mirroring Treeline. During
Milestone 4 the owner chose a native **WPF** UI instead, matching the toolkit used by the owner's
`dotnetlib` and `Relay` repositories.

Two follow-on questions:

1. Reuse `dotnetlib`'s WPF building blocks (MVVM base, theme tokens, controls)?
2. If not, how do we satisfy the rule "WPF projects MUST check `dotnetlib` before adding a local
   duplicate"?

## Decision

- Build the UI in WPF, talking to Core in-process. The loopback API stays for agents/external tools.
- Do **not** take a package/project dependency on `dotnetlib` yet. There is no shared local NuGet feed
  wired into this repository, and a cross-repo reference to the private `dotnetlib` would break
  `dotnet restore` on GitHub and CI.
- Add a minimal local MVVM base (`ObservableObject`, `RelayCommand`) that mirrors `dotnetlib`'s exact
  shape and API, plus local light/dark semantic-token dictionaries and a `ThemeManager`. `dotnetlib`
  was inspected; it exposes the same MVVM primitives but no standalone reusable color-token dictionary
  that could be consumed without referencing its WPF package.

## Consequences

- The repository stays self-contained and restores/builds without an external feed.
- The local MVVM types are a deliberate, documented duplicate. When a shared feed exists, swapping
  `StartupProfiles.App.Mvvm` for `DotNetLib.Core.Mvvm` is a namespace change; the theme tokens can move
  to a shared dictionary at the same time.
- The dev box resolves runtime `10.0.9` for the WPF markup-compile helper but only has `10.0.7`
  installed, so the App project pins `RuntimeFrameworkVersion` to `10.0.7` (roll-forward still runs it
  on newer patches). Remove the pin once a matching runtime is present.
