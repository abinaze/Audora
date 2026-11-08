# Contributing to Audora

Thank you for helping! Audora is free for personal and noncommercial use (PolyForm Noncommercial 1.0.0). You do not need to be an
expert: testing on your own devices is one of the most useful things you can do.

## Ways to help

- **Report a bug.** Tell us which devices were involved (Bluetooth, USB, HDMI, 3.5 mm).
- **Test on your hardware.** Different headphones and adapters behave differently.
  Tell us what worked and what did not.
- **Improve the docs.** Anything confusing is a good reason to fix a page.
- **Fix something or add a feature.** For anything big, open an issue first so we can
  agree on the idea before you spend time on it.

## Set up

You need Windows 10 or 11, Git and the .NET 8 SDK.

```bash
git clone https://github.com/<your-name>/Audora.git
cd Audora
bash scripts/validate.sh      # restore, build and run the tests
bash scripts/dev.sh           # run Audora from source (closes a copy hiding in the tray first)
```

## Making a change

1. Create a branch: `git switch -c fix/short-description`
2. Keep the change small and focused. Add or update tests in `tests/Audora.Tests` when you can.
3. Run `bash scripts/validate.sh`. It must pass.
4. Commit with a clear message, one logical change per commit:
   `feat: add device search`, `fix: handle unplugged USB device`, `docs: clarify install steps`.
   Types: `feat`, `fix`, `docs`, `chore`, `build`, `ci`, `test`, `refactor`, `perf`, `style`.
5. Push and open a pull request. Fill in the checklist.

## Project layout

| Folder | What is in it |
|---|---|
| `src/Audora.Core` | Device detection and the audio engine (no user interface) |
| `src/Audora.App` | The desktop app (Windows, WPF) |
| `tests/Audora.Tests` | Automated tests |
| `tools/Audora.Cli` | Command-line tool for trying and debugging the engine |
| `tools/Audora.IconGen` | Builds the app icon from the logo |
| `assets/brand` | Logo files |
| `docs` | Documentation |

## Ground rules

- Audora stays local: **no accounts, no telemetry, no cloud services.** Changes that add them will not be merged.
- Be kind. See the [Code of Conduct](CODE_OF_CONDUCT.md).
- By contributing you agree your work may be released under the project [license](LICENSE), and that the maintainer may also offer it under other licenses, including commercial ones.
