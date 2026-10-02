# 0002 - Keep the local tray, theme, MVVM, and single-instance code until DotNetLib has a reachable feed

Status: accepted (2026-10-02)

CodePrint now says every Windows tray app uses `DotNetLib.Tray` for the tray icon, menu, theming, and
dialogs (CodePrint `RULES.md` and `docs/ui-and-shared-libraries.md`, section "Windows tray apps").
The same section, under "Open question: how does a repository with CI get the package?", exempts a
repository with CI: `dotnetlib` ships only through a local NuGet feed that a GitHub Actions runner
cannot reach, so such a repository keeps its own tray code until that question is settled. This
repository has CI, so it keeps its own tray (`Tray/TrayIcon.cs`), theme (`Themes/ThemeManager.cs` and
the token dictionaries), MVVM base (`Mvvm/`), and `SingleInstance`, as [ADR 0001](0001-local-wpf-mvvm.md)
already decided for MVVM and theming. It moves to `DotNetLib.Tray` and the other DotNetLib packages
once they are published to a feed that CI can restore from.
