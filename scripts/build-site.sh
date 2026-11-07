#!/usr/bin/env bash
# Builds the website into _site/. The logo shapes are read from assets/brand and put straight into the page,
# so the logo can never be missing. Screenshots in assets/screenshots are used when they exist.
set -euo pipefail
cd "$(dirname "$0")/.."

for f in assets/brand/mark.svg assets/brand/wordmark-dark.svg site/index.html site/style.css site/main.js; do
  [ -f "$f" ] || { echo "Missing $f"; exit 1; }
done

shape() { sed -n "s|.*fill=\"$1\" d=\"\([^\"]*\)\".*|\1|p" assets/brand/mark.svg | head -n 1; }
MARK_DARK="$(shape '#351B9F')"
MARK_BLUE="$(shape '#1676FC')"
MARK_PURPLE="$(shape '#7E33F5')"
WORDMARK="$(sed -n 's|.*d="\([^"]*\)".*|\1|p' assets/brand/wordmark-dark.svg | head -n 1)"
if [ -z "$MARK_DARK" ] || [ -z "$MARK_BLUE" ] || [ -z "$MARK_PURPLE" ] || [ -z "$WORDMARK" ]; then
  echo "Could not read the logo files in assets/brand."
  exit 1
fi

HERO=site/snippets/hero-mock.html
[ -f assets/screenshots/devices.png ] && HERO=site/snippets/hero-shot.html
SHOTS=/dev/null
if [ -f assets/screenshots/sound.png ] && [ -f assets/screenshots/groups.png ]; then
  SHOTS=site/snippets/shots.html
fi

rm -rf _site
mkdir -p _site

awk -v hero="$HERO" -v shots="$SHOTS" '
/\{\{HERO\}\}/  { while ((getline line < hero)  > 0) print line; close(hero);  next }
/\{\{SHOTS\}\}/ { while ((getline line < shots) > 0) print line; close(shots); next }
{ print }' site/index.html > _site/index.html

sed -i "s|{{MARK_DARK}}|$MARK_DARK|; s|{{MARK_BLUE}}|$MARK_BLUE|; s|{{MARK_PURPLE}}|$MARK_PURPLE|; s|{{WORDMARK}}|$WORDMARK|" _site/index.html

cp site/style.css site/main.js _site/
cp assets/brand/mark.svg _site/favicon.svg
if [ -d assets/screenshots ]; then
  mkdir -p _site/assets/screenshots
  cp assets/screenshots/*.png _site/assets/screenshots/ 2>/dev/null || true
fi
touch _site/.nojekyll

echo "Website built in _site/ ($(basename "$HERO"))"
