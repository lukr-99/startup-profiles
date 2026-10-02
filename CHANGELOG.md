# Changelog

Every release, newest first. The version lives in `VersionPrefix` in `Directory.Build.props`, and a
release is a Git tag `vX.Y.Z` that matches it.

## [Unreleased]

Nothing is released yet. The first release will be 0.1.0, with this baseline:

- A login launcher that asks what you are about to do and runs only that profile's actions: apps,
  URLs, scripts, folders, services, VPN, delays, waits, and kills.
- A configuration window with Profiles and Settings tabs, emoji profile icons, and light, dark, and
  system themes. The system theme follows Windows while the app runs.
- The base: actions that run before every profile, unless a profile opts out with **Include base**.
- A global library of startable items. Profile rows link to library items, and a side panel offers
  the apps Windows starts at login and lets you drag them into profiles.
- Startup takeover: the installer moves the apps Windows starts at login into the Everything profile
  and switches them off in Windows. Uninstall switches them back on.
- A tray icon to switch profiles, a loopback HTTP API for agents and tools, and a bundled agent skill.
- The integration contract: other apps ask to be added through a `startupprofiles://` link, the CLI,
  or the API, and the user decides in a Startup Profiles window.
- A per-user `install/install.ps1` and `install/uninstall.ps1`, and a tag-driven release workflow.
