#!/usr/bin/env bash
# Gets the Studio noise removal files ready: the DeepFilterNet3 library, its model and their licence texts.
# They come from the deepfilternet-slim 0.1.0 Windows wheel on PyPI. Nothing is used until its SHA256
# matches third-party/studio.sha256, so a changed or damaged file stops the build.
#
# Usage:  scripts/studio.sh <folder named studio>
# Where the wheel comes from, in this order:
#   1. the file named in AUDORA_STUDIO_WHEEL
#   2. .cache/studio/ in this repository (git-ignored)
#   3. pip download, when Python is installed (this is what the release build does)
set -euo pipefail
cd "$(dirname "$0")/.."

OUT="${1:-}"
[ -n "$OUT" ] || { echo "Usage: scripts/studio.sh <folder named studio>"; exit 1; }
[ "$(basename "$OUT")" = "studio" ] || { echo "The output folder must be named studio."; exit 1; }

PINS="third-party/studio.sha256"
VERSION="0.1.0"
WHEEL_NAME="deepfilternet_slim-$VERSION-py3-none-win_amd64.whl"
CACHE=".cache/studio"
[ -f "$PINS" ] || { echo "$PINS is missing."; exit 1; }

pinned() { # pinned <name>: the SHA256 listed for that name
  tr -d '\r' < "$PINS" | awk -v n="$1" '$1 !~ /^#/ && ($2 == n || $2 == "*" n) { print $1; exit }'
}

check() { # check <name> <file>: stop unless the file has the pinned SHA256
  local want got
  want="$(pinned "$1")"
  [ -n "$want" ] || { echo "No checksum is listed for $1 in $PINS."; exit 1; }
  got="$(sha256sum "$2" | cut -d' ' -f1)"
  if [ "$got" != "$want" ]; then
    echo "$1 does not match its pinned checksum, so it is not used."
    echo "  expected $want"
    echo "  found    $got"
    exit 1
  fi
}

WHEEL="${AUDORA_STUDIO_WHEEL:-}"
if [ -z "$WHEEL" ] && [ -f "$CACHE/$WHEEL_NAME" ]; then
  WHEEL="$CACHE/$WHEEL_NAME"
fi
if [ -z "$WHEEL" ]; then
  for py in python3 python py; do
    command -v "$py" >/dev/null 2>&1 || continue
    mkdir -p "$CACHE"
    if "$py" -m pip download "deepfilternet-slim==$VERSION" --only-binary=:all: --no-deps --platform win_amd64 \
         --dest "$CACHE" --no-input --disable-pip-version-check --retries 2 --timeout 20 >/dev/null 2>&1 \
       && [ -f "$CACHE/$WHEEL_NAME" ]; then
      WHEEL="$CACHE/$WHEEL_NAME"
      break
    fi
  done
fi
if [ -z "$WHEEL" ] || [ ! -f "$WHEEL" ]; then
  echo "The DeepFilterNet wheel was not found."
  echo "Download $WHEEL_NAME from https://pypi.org/project/deepfilternet-slim/$VERSION/#files"
  echo "then run again with:  AUDORA_STUDIO_WHEEL=/path/to/$WHEEL_NAME bash scripts/studio.sh <folder named studio>"
  exit 1
fi

check "$WHEEL_NAME" "$WHEEL"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

mkdir -p "$TMP/wheel"
if command -v unzip >/dev/null 2>&1; then
  unzip -q -o "$WHEEL" -d "$TMP/wheel"
elif command -v powershell.exe >/dev/null 2>&1; then
  cp "$WHEEL" "$TMP/wheel.zip"
  powershell.exe -NoProfile -Command "Expand-Archive -LiteralPath '$(cygpath -w "$TMP/wheel.zip")' -DestinationPath '$(cygpath -w "$TMP/wheel")' -Force"
else
  echo "Neither unzip nor PowerShell was found, so the wheel cannot be opened."
  exit 1
fi

SRC="$TMP/wheel/deepfilternet_slim"
DIST="$TMP/wheel/deepfilternet_slim-$VERSION.dist-info"
for f in "$SRC/_native/deepfilter.dll" "$SRC/_models/DeepFilterNet3_onnx.tar.gz" \
         "$SRC/_licenses/LICENSE" "$SRC/_licenses/LICENSE-APACHE" "$SRC/_licenses/LICENSE-MIT" \
         "$DIST/licenses/NOTICE"; do
  [ -f "$f" ] || { echo "The wheel does not hold the expected file: ${f#"$TMP"/wheel/}"; exit 1; }
done

check deepfilter.dll "$SRC/_native/deepfilter.dll"
check DeepFilterNet3_onnx.tar.gz "$SRC/_models/DeepFilterNet3_onnx.tar.gz"

mkdir -p "$OUT"
cp "$SRC/_native/deepfilter.dll" "$SRC/_models/DeepFilterNet3_onnx.tar.gz" "$OUT/"
cp "$SRC/_licenses/LICENSE" "$SRC/_licenses/LICENSE-APACHE" "$SRC/_licenses/LICENSE-MIT" "$DIST/licenses/NOTICE" "$OUT/"

echo "Studio files are ready in $OUT and match the pinned checksums:"
ls -l "$OUT" | awk 'NR > 1 { print "  " $5 "  " $9 }'
