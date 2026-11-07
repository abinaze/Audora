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
