# Audora website

A small static site: plain HTML, CSS and JavaScript. `bash scripts/build-site.sh` builds it into `_site/`.
The "Website" workflow publishes it with GitHub Pages (started by hand from the Actions tab).

```bash
bash scripts/build-site.sh
start _site/index.html
```

- `index.html` is a template. The build puts the logo shapes (read from `assets/brand`) and the
  first-screen picture into it.
- Screenshots: save `devices.png`, `sound.png` and `groups.png` in `assets/screenshots/` and build again.
  `devices.png` becomes the picture in the first screen; `sound.png` and `groups.png` together add a
  "A look inside" section. Without them the site shows a simple drawing.
- The download buttons always point at the latest release (`Audora-Setup.exe` and `Audora-Portable.zip`).
- The site address comes from `SITE_URL` (default `https://abinaze.github.io/Audora`). Set it when the site is
  served from another address, for example `SITE_URL=https://example.com bash scripts/build-site.sh`.
- The build also writes `404.html`, `robots.txt` and `sitemap.xml`, then checks the pages: no placeholder left,
  every `#link` has a target, nothing loads from another site, and the JSON-LD block is valid.
- `bash scripts/preview-site.sh` builds the site for this computer and shows it at `http://localhost:8080`
  (needs Node.js). Use it instead of opening the file, because the pages carry a content policy.
