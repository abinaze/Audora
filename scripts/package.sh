#!/usr/bin/env bash
# Builds the Windows downloads into artifacts/:
#   Audora-Portable.zip   unzip anywhere and run Audora.exe
#   Audora-Setup.exe      the installer (needs Inno Setup, see docs/RELEASING.md)
#   SHA256SUMS.txt        checksums, so people can check the files
# Usage: scripts/package.sh [version]     (default: the version in Directory.Build.props)
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="${1:-$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' Directory.Build.props | head -n 1)}"
OUT="artifacts"
PUBLISH="$OUT/publish"
PORTABLE="$OUT/portable/Audora"

command -v dotnet >/dev/null || { echo "The .NET SDK is not installed."; exit 1; }

if [ "${SKIP_VALIDATE:-0}" != "1" ]; then
  bash scripts/validate.sh
fi

rm -rf "$OUT"
mkdir -p "$PORTABLE"

echo "==> Publishing Audora $VERSION (self-contained, 64-bit)"
dotnet publish src/Audora.App -c Release -r win-x64 --self-contained true \
  -p:Version="$VERSION" -p:DebugType=none -p:DebugSymbols=false -o "$PUBLISH" --nologo -v q
[ -f "$PUBLISH/Audora.exe" ] || { echo "Audora.exe was not produced."; exit 1; }

echo "==> Portable zip"
cp -r "$PUBLISH"/. "$PORTABLE"/
cp LICENSE "$PORTABLE/LICENSE.txt"
cat > "$PORTABLE/README.txt" <<TXT
Audora $VERSION (portable)

Run Audora.exe. Nothing is installed and nothing is added to Windows.
Your settings are saved in %LOCALAPPDATA%\Audora.
Free for personal and noncommercial use. See LICENSE.txt.
https://github.com/abinaze/Audora
TXT
powershell.exe -NoProfile -Command "Compress-Archive -Path '$(cygpath -w "$PORTABLE")' -DestinationPath '$(cygpath -w "$OUT/Audora-Portable.zip")' -Force"

echo "==> Installer"
ISCC=""
for candidate in \
  "$(command -v ISCC.exe 2>/dev/null || true)" \
  "/c/Program Files (x86)/Inno Setup 6/ISCC.exe" \
  "/c/Program Files/Inno Setup 6/ISCC.exe" \
  "$(cygpath -u "${LOCALAPPDATA:-/nonexistent}" 2>/dev/null || true)/Programs/Inno Setup 6/ISCC.exe"; do
  if [ -n "$candidate" ] && [ -f "$candidate" ]; then
    ISCC="$candidate"
    break
  fi
done

if [ -z "$ISCC" ]; then
  echo "Inno Setup was not found, so the installer was skipped."
  echo "Install it with:  winget install --id JRSoftware.InnoSetup -e   then run this again."
else
  MSYS2_ARG_CONV_EXCL='*' "$ISCC" /Qp "/DAppVersion=$VERSION" \
    "/DSourceDir=$(cygpath -w "$PUBLISH")" "/DOutputDir=$(cygpath -w "$OUT")" \
    "$(cygpath -w installer/Audora.iss)"
fi

echo "==> Checksums"
( cd "$OUT" && sha256sum Audora-Portable.zip $( [ -f Audora-Setup.exe ] && echo Audora-Setup.exe ) > SHA256SUMS.txt )

echo
ls -lh "$OUT"/Audora-* "$OUT"/SHA256SUMS.txt
echo "Done."
