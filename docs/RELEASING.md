# Making a release

For maintainers. A release builds the installer and the portable zip and publishes them on GitHub.

## One time

- Install [Inno Setup 6](https://jrsoftware.org/isinfo.php) if you want to build the installer on your own
  computer: `winget install --id JRSoftware.InnoSetup -e`. GitHub installs it itself when it builds a release.

## Before you rebuild

Quit Audora first (right-click its tray icon, **Quit Audora**), or the build cannot replace its files.

## Try the build on your computer

```bash
bash scripts/package.sh
```

This runs the tests, then creates `artifacts/Audora-Portable.zip`, `artifacts/Audora-Setup.exe` and
`artifacts/SHA256SUMS.txt`. Install it and run it before every release.

## Publish a release

1. Put what changed under `## [Unreleased]` in `CHANGELOG.md`.
2. Run `bash scripts/release.sh 0.2.0` (use your new version number).
   It runs the tests, sets the version, dates the changelog entry, creates the tag and asks before pushing.
3. GitHub builds and publishes the files. Watch it in the **Actions** tab. The website's download buttons
   follow the newest release automatically.

The release notes are the changelog section for that version, followed by the download list.

## Versions

Audora uses [semantic versioning](https://semver.org): `MAJOR.MINOR.PATCH`. While the major number is 0 the
app is still young and things may change.

## Old versions stay as they are

Every release is a new version number. A released version is never replaced or tagged again.
`scripts/release.sh` refuses a number that is not higher than the newest tag, and GitHub refuses a second
release for the same tag. Installing a newer Audora over an older one updates it and keeps your settings.
