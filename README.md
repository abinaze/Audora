<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/logo-dark.svg">
    <img alt="Audora" src="assets/brand/logo-light.svg" width="280">
  </picture>
</p>

<p align="center">
  <b>Play your Windows audio on many speakers and headphones at once.</b><br>
  Free, open source and private.
</p>

<p align="center">
  <a href="https://github.com/abinaze/Audora/actions/workflows/build.yml"><img alt="Build" src="https://github.com/abinaze/Audora/actions/workflows/build.yml/badge.svg"></a>
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-blue">
  <img alt="Platform: Windows 10 and 11" src="https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078D6">
  <img alt="Status: early development" src="https://img.shields.io/badge/status-early%20development-orange">
</p>

---

## What is Audora?

Windows normally plays sound on one device at a time. Audora lets you choose several
(Bluetooth headphones, USB speakers, wired headphones, HDMI screens) and plays the same
audio on all of them together. Each device gets its own volume, so turning one down
never changes the others.

## Status

**Early development.** The audio engine already works on real hardware and you can try it
from the command line. The desktop app window, the installer and the website are still
being built, so Audora is not ready for everyday use yet.

## What works today

- Finds every Windows audio output and tells you if it is Bluetooth, USB, HDMI or wired
- Plays your system audio on several devices at the same time
- Separate volume, mute, left/right balance and delay for each device
- Keeps playing when one device disconnects, and brings it back when it returns
- Follows the Windows default output when you change it

## Coming next

- A simple desktop app with a card and sliders for every device
- Saved device groups, a system tray icon, dark and light themes
- An installer and a portable download

## Try it

You need Windows 10 or 11, [Git](https://git-scm.com/) and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/abinaze/Audora.git
cd Audora
bash scripts/validate.sh

# see your audio devices
dotnet run --project tools/Audora.Cli -- list

# play your audio on several devices (use part of each name shown by "list")
dotnet run --project tools/Audora.Cli -- run "<device name>" "<another device name>"
```

Start some music first. While it runs, type `s` to see each device, `v 1 40` to set the
volume of device 1 to 40%, and `q` to quit. Having trouble? See
[Troubleshooting](docs/TROUBLESHOOTING.md).

## How it works

Windows cannot send one app's sound to two devices by itself. Audora listens to what your
default device is playing and copies it to the other devices you pick. The default device
plays directly, so the others can be a little behind (Bluetooth most of all). Audora's
per-device delay lines them up. More detail in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Privacy

No account, no telemetry, no cloud, no ads. Audio is never recorded or sent anywhere.
Read the short [privacy note](PRIVACY.md).

## Contributing

Bug reports, testing on your own devices, documentation fixes and code are all welcome.
Start with [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE). Built with [NAudio](https://github.com/naudio/NAudio).
