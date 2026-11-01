#!/usr/bin/env bash
# Restore, build and test the whole solution. Run before every commit.
set -euo pipefail
cd "$(dirname "$0")/.."

command -v dotnet >/dev/null || { echo "dotnet SDK not found"; exit 1; }

SLN=""
for f in Audora.slnx Audora.sln; do
  if [ -f "$f" ]; then SLN="$f"; break; fi
done
[ -n "$SLN" ] || { echo "No solution file found"; exit 1; }

echo "==> Restore";         dotnet restore "$SLN" --nologo -v q
echo "==> Build (Release)"; dotnet build "$SLN" -c Release --no-restore --nologo -v q
echo "==> Test";            dotnet test "$SLN" -c Release --no-build --nologo -v q
echo "Validation passed."
