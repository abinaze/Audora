# Audora website

A small static site (plain HTML, CSS and JavaScript, no build tools). It is published with GitHub Pages
by the "Website" workflow, which you start by hand from the Actions tab.

Preview it on your computer:

```bash
bash scripts/build-site.sh
start _site/index.html
```

- The logos come from `assets/brand` and are copied in by the build script.
- Screenshots: add `devices.png`, `sound.png` and `groups.png` to `assets/screenshots/`. A "See it" section
  appears by itself when the pictures exist.
- The download buttons always point at the latest release (`Audora-Setup.exe` and `Audora-Portable.zip`).
