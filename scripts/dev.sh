#!/usr/bin/env bash
# Runs Audora from source. A copy hiding in the tray locks the program files, so close any running copy first.
set -euo pipefail
cd "$(dirname "$0")/.."

for name in Audora.exe Audora.App.exe; do
  MSYS2_ARG_CONV_EXCL='*' taskkill /F /IM "$name" >/dev/null 2>&1 || true
done
sleep 1

dotnet run --project src/Audora.App
