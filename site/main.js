// Fills in the version and download sizes from the latest GitHub release.
// The buttons already work without this: they point at the latest release files.
(async () => {
  try {
    const response = await fetch("https://api.github.com/repos/abinaze/Audora/releases/latest", {
      headers: { Accept: "application/vnd.github+json" },
    });
    if (!response.ok) return;

    const release = await response.json();
    const version = String(release.tag_name || "").replace(/^v/, "");
    if (version) {
      document.querySelectorAll("[data-version]").forEach((el) => {
        el.textContent = `Version ${version}`;
      });
    }

    const assets = Array.isArray(release.assets) ? release.assets : [];
    const find = (pattern) => assets.find((a) => pattern.test(a.name));
    const kinds = { installer: find(/setup.*\.exe$/i), portable: find(/portable.*\.zip$/i) };

    for (const [kind, asset] of Object.entries(kinds)) {
      if (!asset) continue;
      document.querySelectorAll(`[data-download="${kind}"]`).forEach((a) => {
        a.href = asset.browser_download_url;
      });
      document.querySelectorAll(`[data-size="${kind}"]`).forEach((el) => {
        el.textContent = `${Math.max(1, Math.round(asset.size / 1048576))} MB`;
      });
    }
  } catch {
    // offline or rate limited: the plain links still work
  }
})();
