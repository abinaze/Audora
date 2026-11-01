#!/usr/bin/env bash
# Validate, commit and push one logical change.
# Usage: scripts/ship.sh "type: message" [paths...]
#   No paths = stage everything.  SKIP_VALIDATE=1 skips build/test (docs-only changes).
set -euo pipefail
cd "$(dirname "$0")/.."

MSG="${1:-}"
[ -n "$MSG" ] || { echo 'Usage: scripts/ship.sh "type: message" [paths...]'; exit 1; }
shift

if ! echo "$MSG" | grep -Eq '^(feat|fix|docs|chore|build|ci|test|refactor|perf|style)(\([a-z0-9-]+\))?: .+'; then
  echo "ERROR: message must look like 'feat: add device list' (types: feat fix docs chore build ci test refactor perf style)"
  exit 1
fi

if [ "$#" -gt 0 ]; then git add -- "$@"; else git add -A; fi

if git diff --cached --quiet; then
  echo "Nothing staged. Refusing to make an empty commit."
  exit 1
fi

echo "Staged changes:"
git diff --cached --stat

if [ "${SKIP_VALIDATE:-0}" != "1" ]; then
  bash scripts/validate.sh
fi

git commit -m "$MSG"
git push origin HEAD
