# Pitfalls

Mistakes this repository has already made, kept short so they are not made twice. When something
fails in a way you did not expect, search here for the error text before debugging it.

Add an entry in the same commit as the fix when a bug took longer to find than to fix, came back, or
came from a tool or platform trap. Put new entries at the top, in this shape:

```markdown
## What goes wrong, in a few words

- Symptom: the exact error text, or what you see
- Cause: why it happens
- Fix: what to do about it
- Closed off by: the test, check, or tool that now catches it, or "not yet"
- Seen: date and where (milestone, PR, or commit)
```

A pitfall that could hit another repository is also reported to CodePrint. CodePrint's
`docs/pitfalls/README.md` explains how.

## The themed combo box ignores DisplayMemberPath

- Symptom: a combo box bound to records shows `Choice { Value = Default, Label = Ask me }` as the
  selected text, while the open list looks fine.
- Cause: the `ComboBox` template in `Themes/Controls.xaml` shows the selection through a plain
  `ContentPresenter` on `SelectionBoxItem`, which falls back to `ToString()` and skips
  `DisplayMemberPath`.
- Fix: give option records a `ToString()` that returns the label (`Config/Choice`,
  `Discovery/PlacementOption`), or bind the template's presenter to `SelectionBoxItemTemplate`.
- Closed off by: not yet (checked by offscreen renders only)
- Seen: 2026-10-02, profile editor redesign (commit d1a4cc5)

## Emoji buttons draw black on the dark theme

- Symptom: the icon picker's emoji are almost invisible in dark mode.
- Cause: WPF draws emoji in one color (no color font support). A button style without a
  `Foreground` setter gives them the default black.
- Fix: set `Foreground` to `App.Text` in any style that shows emoji (`App.EmojiButton`), and check
  dark mode in a render.
- Closed off by: not yet
- Seen: 2026-10-02, icon picker (commit d1a4cc5)

## A packaged app's shell sees a different AppData and HKCU

- Symptom: `install/install.ps1` reports success, but the app is not in
  `%LOCALAPPDATA%\Programs\StartupProfiles`, nothing starts at login, and the data files read from
  `%APPDATA%\StartupProfiles` are old or missing compared with what the running app shows.
- Cause: a process started inside a packaged (MSIX) app, such as the Claude desktop app and every
  shell or agent it starts, has AppData and some HKCU writes redirected into a private copy under
  `%LOCALAPPDATA%\Packages\<package family>\LocalCache\...`. The install lands there, and reads see
  that copy instead of the real data folder.
- Fix: run `install.ps1`, `uninstall.ps1`, and anything that reads or writes the app's data or HKCU
  from a normal terminal, or start it through `explorer.exe` to leave the package context. Read live
  data through the loopback API (`GET /api/...`, see [API.md](API.md)), which is not redirected.
  [HANDOFF.md](../HANDOFF.md) (Build, test, run) has the same note.
- Closed off by: not yet
- Seen: 2026-09-18, commit 5a88af7 (documented in HANDOFF.md)

## Two resources with one key crash the app on startup

- Symptom: `InvalidOperationException` with `'System.Windows.Style' is not a valid value for
  property 'Background'` as soon as any `ScrollBar` renders. The installed app died during the first
  launcher `Show`. Before the cause was found it looked like a crash on resize.
- Cause: the scrollbar `Thumb` style and the scroll thumb color token both used the key
  `App.ScrollThumb`. `Themes/Controls.xaml` is merged after the theme token dictionaries, so the style
  shadowed the brush, and `Background="{DynamicResource App.ScrollThumb}"` resolved to a `Style`.
- Fix: give styles and brushes distinct keys. The thumb style is now `App.ScrollThumbStyle`. The
  global dispatcher exception handler writes such crashes to `error.log`, which is how this was found.
- Closed off by: not yet (no test checks for duplicate resource keys across merged dictionaries)
- Seen: 2026-09-01, commit 5bdc5df (after commit 33b9c46 fixed the wrong suspect, the horizontal
  scrollbar track orientation)

## The WPF markup compile asks for a runtime that is not installed

- Symptom: building `StartupProfiles.App` fails in the WPF markup-compile step because it asks for
  the shared runtime `10.0.9`, while the dev box only has `10.0.7` installed. The exact message was
  not recorded.
- Cause: the SDK resolves a newer patch runtime for the WPF markup-compile helper than the one
  installed on the machine.
- Fix: `src/StartupProfiles.App/StartupProfiles.App.csproj` pins
  `<RuntimeFrameworkVersion>10.0.7</RuntimeFrameworkVersion>`. Roll-forward still runs the app on
  newer patches. Remove the pin once the machine has a matching runtime. See
  [ADR 0001](adr/0001-local-wpf-mvvm.md).
- Closed off by: not yet
- Seen: 2026-08-31, commit 7cda859
