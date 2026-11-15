# Going public checklist

Use this when you decide to make the repository public.

## Before you flip the switch

- [ ] `LICENSE` is the one you want (PolyForm Noncommercial: free for personal use, commercial use needs permission).
- [ ] Read through the [Git history](https://github.com/abinaze/Audora/commits/main) for anything private
      (passwords, tokens, personal paths). Nothing like that should be in this project.
- [ ] `README.md`, `PRIVACY.md`, `SECURITY.md` and `docs/` say what you want them to.
- [ ] You tried `bash scripts/package.sh` and installed the result on a clean user account.
- [ ] `THIRD-PARTY-NOTICES.md` matches the licence text of each project it lists.
- [ ] Optional: add screenshots (`devices.png`, `sound.png`, `groups.png`) to `assets/screenshots/`.

## Switch it on

- [ ] Settings → General → Danger Zone → **Change visibility → Make public**.
- [ ] Settings → General → Social preview → upload `assets/brand/social-preview.png`.
- [ ] Settings → Pages → Source: **GitHub Actions**, then run the **Website** workflow from the Actions tab.
- [ ] Settings → Code security → turn on **Private vulnerability reporting**.
- [ ] Settings → General → Features: Discussions on, Wikis off.
- [ ] Run `bash scripts/release.sh <new version>` to publish the first public release. The number must be higher than v0.1.0.
- [ ] Check the website's download buttons work, and set the website address in the repository's **About** box.

## Later

- Code-sign the installer, to remove the SmartScreen warning.
- Add Audora to winget so people can install it with one command.
