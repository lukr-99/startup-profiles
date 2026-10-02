# 0003 - Adopt DotNetLib.Core and DotNetLib.Tray

Status: accepted (2026-10-02). Supersedes [ADR 0002](0002-local-tray-until-dotnetlib-feed.md) and the
local-copy part of [ADR 0001](0001-local-wpf-mvvm.md).

## Context

ADR 0002 kept local tray, theme, MVVM, and single-instance code because DotNetLib shipped only through
a local NuGet feed that CI could not reach. `dotnetlib` now publishes `DotNetLib.Core` and
`DotNetLib.Tray` to GitHub Packages on a version tag (0.2.0), and dotnetlib's README documents how an
app restores them locally and in CI. CodePrint names `DotNetLib.Tray` the standard for Windows tray
apps (`RULES.md`, `docs/ui-and-shared-libraries.md`, "Windows tray apps"), and its update seam
follows `DotNetLib.Core.Updating` (`docs/delivery-and-updates.md`).

## Decision

The App references `DotNetLib.Core` and `DotNetLib.Tray` 0.2.0 from the `dotnetlib` source in
`nuget.config`. Locally the token sits in the user NuGet config under that source name; CI passes the
`DOTNETLIB_PACKAGES_TOKEN` secret (a classic PAT with `read:packages`) through
`NuGetPackageSourceCredentials_dotnetlib`.

- MVVM: `DotNetLib.Core.Mvvm` replaces the local `Mvvm/` copies (same API).
- Updates: `DotNetLib.Core.Updating` (`GitHubReleaseSource`, `ReleaseVersion`, `UpdateService`) under
  `App/Updates/UpdateCoordinator`, which adds the SHA-256 check CodePrint requires.
- Theme: `TrayThemeApplier` decides light, dark, or high contrast and follows Windows. The app keeps its
  own `App.*` token dictionaries and `Controls.xaml` on top, so its look does not change.
- Tray: `TrayIconHost` and `TrayMenuBuilder` replace the WinForms `NotifyIcon` and custom renderer; the
  menu's contents come from the WPF-free, tested `Tray/TrayMenuModel`. WinForms is no longer used.
- Single instance: `DotNetLib.Tray.SingleInstance` (per user, named pipe) replaces the local mutex and
  event.

## Consequences

- A fresh clone needs the GitHub Packages credential before `dotnet restore` works (README, Requirements).
- WPF UI's implicit styles now apply to controls the app does not style itself (tooltips, menus). An
  app template that hosts a bare `ToggleButton` must opt out with `Style="{x:Null}"`
  ([pitfalls](../pitfalls.md)).
- The kit's `TrayMessageWindow` and `TrayTextWindow` are not used yet; `Interaction/UserPrompts` keeps
  the app's own themed dialogs, which carry more layouts (icon picker, backup preview).
