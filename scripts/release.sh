#!/usr/bin/env bash
# Creates a release: checks everything, sets the version, updates the changelog, tags it and (after you
# confirm) pushes. GitHub then builds the installer and portable zip and publishes them.
# Usage: scripts/release.sh 1.0.0
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="${1:-}"
if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Usage: scripts/release.sh <version>   for example: scripts/release.sh 1.0.0"
  exit 1
fi

[ "$(git rev-parse --abbrev-ref HEAD)" = "main" ] || { echo "Switch to the main branch first."; exit 1; }
[ -z "$(git status --porcelain)" ] || { echo "You have uncommitted changes. Commit or stash them first."; exit 1; }
git pull --ff-only
# A release is always a new, higher version. An existing release is never replaced.
git fetch --tags --quiet
LATEST="$(git tag --list 'v*' | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | sed 's/^v//' | sort -V | tail -n 1 || true)"
if [ -n "$LATEST" ] && [ "$VERSION" != "$LATEST" ] && [ "$(printf '%s\n%s\n' "$LATEST" "$VERSION" | sort -V | tail -n 1)" != "$VERSION" ]; then
  echo "Version $VERSION is lower than the latest release v$LATEST."
  echo "Every release gets a new, higher number. Old releases are never replaced."
  exit 1
fi
if git rev-parse "v$VERSION" >/dev/null 2>&1; then
  echo "The tag v$VERSION already exists. Releases are never replaced, so use the next version number."
  exit 1
fi

bash scripts/validate.sh

sed -i -E "s|<Version>[^<]+</Version>|<Version>$VERSION</Version>|" Directory.Build.props
sed -i "s|^## \[Unreleased\]|## [Unreleased]\n\n## [$VERSION] - $(date +%Y-%m-%d)|" CHANGELOG.md

git add -A
git commit -m "chore: release v$VERSION"
git tag -a "v$VERSION" -m "Audora $VERSION"

echo
echo "Created v$VERSION on this computer."
read -r -p "Push it to GitHub now, which starts the release build? [y/N] " answer
if [ "$answer" = "y" ] || [ "$answer" = "Y" ]; then
  git push origin main
  git push origin "v$VERSION"
  echo "Pushed. Watch the build at https://github.com/abinaze/Audora/actions"
else
  echo "Not pushed. When ready:  git push origin main && git push origin v$VERSION"
fi
