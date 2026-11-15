#!/usr/bin/env bash
# Builds the website into _site/ and checks it. The logo shapes are read from assets/brand and put straight
# into the pages, so the logo can never be missing. Screenshots in assets/screenshots are used when they exist.
# SITE_URL sets the public address of the site (default: https://abinaze.github.io/Audora).
set -euo pipefail
cd "$(dirname "$0")/.."

SITE_URL="${SITE_URL:-https://abinaze.github.io/Audora}"
SITE_URL="${SITE_URL%/}"
if ! [[ "$SITE_URL" =~ ^https?://[A-Za-z0-9._:/-]+$ ]]; then
  echo "SITE_URL must look like https://example.com or https://example.com/Audora"
  exit 1
fi

for f in assets/brand/mark.svg assets/brand/wordmark-dark.svg assets/brand/social-preview.png \
         site/index.html site/404.html site/style.css site/main.js; do
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
cp site/404.html _site/404.html

for page in _site/index.html _site/404.html; do
  sed -i "s|{{MARK_DARK}}|$MARK_DARK|; s|{{MARK_BLUE}}|$MARK_BLUE|; s|{{MARK_PURPLE}}|$MARK_PURPLE|; s|{{WORDMARK}}|$WORDMARK|; s|{{SITE_URL}}|$SITE_URL|g" "$page"
done

cp site/style.css site/main.js _site/
cp assets/brand/mark.svg _site/favicon.svg
cp assets/brand/social-preview.png _site/social-preview.png
if [ -d assets/screenshots ]; then
  mkdir -p _site/assets/screenshots
  cp assets/screenshots/*.png _site/assets/screenshots/ 2>/dev/null || true
fi
printf 'User-agent: *\nAllow: /\n\nSitemap: %s/sitemap.xml\n' "$SITE_URL" > _site/robots.txt
printf '<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n  <url>\n    <loc>%s/</loc>\n    <lastmod>%s</lastmod>\n  </url>\n</urlset>\n' "$SITE_URL" "$(date +%Y-%m-%d)" > _site/sitemap.xml
touch _site/.nojekyll

echo "==> Checking the site"
fail=0
if grep -rn '{{' _site --include='*.html'; then
  echo "A placeholder was left in a page."
  fail=1
fi
for page in _site/index.html _site/404.html; do
  for id in $(grep -o 'href="#[^"]*"' "$page" | sed 's/^href="#//; s/"$//' | sort -u); do
    [ -n "$id" ] || continue
    grep -q "id=\"$id\"" "$page" || { echo "$page: a link to #$id has no matching id"; fail=1; }
  done
done
if grep -n 'src="http' _site/*.html; then
  echo "A page loads a script or image from another site. The content policy blocks that."
  fail=1
fi
for f in index.html 404.html style.css main.js favicon.svg social-preview.png robots.txt sitemap.xml .nojekyll; do
  [ -f "_site/$f" ] || { echo "Missing _site/$f"; fail=1; }
done
if command -v node >/dev/null 2>&1 && grep -q 'application/ld+json' _site/index.html; then
  awk '/<script type="application\/ld\+json">/ { p = 1; next } /<\/script>/ { p = 0 } p' _site/index.html \
    | node -e 'JSON.parse(require("fs").readFileSync(0, "utf8"))' 2>/dev/null \
    || { echo "The JSON-LD block in index.html is not valid JSON."; fail=1; }
fi
[ "$fail" = 0 ] || { echo "The site checks failed."; exit 1; }

echo "Website built in _site/ ($(basename "$HERO"), address $SITE_URL)"
