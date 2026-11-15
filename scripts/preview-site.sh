#!/usr/bin/env bash
# Builds the website for this computer and shows it at http://localhost:8080. Needs Node.js.
# Press Ctrl+C to stop. Use PORT=9000 bash scripts/preview-site.sh for another port.
set -euo pipefail
cd "$(dirname "$0")/.."

command -v node >/dev/null || { echo "Node.js is not installed."; exit 1; }
PORT="${PORT:-8080}"

SITE_URL="http://localhost:$PORT" bash scripts/build-site.sh
echo "Open http://localhost:$PORT in your browser. Press Ctrl+C to stop."
( sleep 1; start "http://localhost:$PORT" ) >/dev/null 2>&1 &
exec node scripts/preview-site.js "$PORT"
