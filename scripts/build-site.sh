#!/usr/bin/env bash
# Builds the website into _site/ (the site plus the logos and any screenshots).
set -euo pipefail
cd "$(dirname "$0")/.."

rm -rf _site
mkdir -p _site/assets/brand
cp -r site/. _site/
cp assets/brand/*.svg _site/assets/brand/
if [ -d assets/screenshots ]; then
  mkdir -p _site/assets/screenshots
  cp assets/screenshots/*.png _site/assets/screenshots/ 2>/dev/null || true
fi
touch _site/.nojekyll
echo "Website built in _site/"
