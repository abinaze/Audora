<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/logo-dark.svg">
    <img alt="Audora" src="assets/brand/logo-light.svg" width="280">
  </picture>
</p>

<p align="center">
  <b>Play your Windows audio on many speakers and headphones at once.</b><br>
  Free for personal use. Private by design.
</p>

<p align="center">
  <a href="https://github.com/abinaze/Audora/actions/workflows/build.yml"><img alt="Build" src="https://github.com/abinaze/Audora/actions/workflows/build.yml/badge.svg"></a>
  <img alt="License: PolyForm Noncommercial" src="https://img.shields.io/badge/license-PolyForm%20Noncommercial-blue">
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

**Early development.** The app already works on real hardware. The installer and the
download website are still being built, so for now you run it from source.

## What works today

- One window with a card for every connected audio device
- Play on as many devices as you like, switch them on and off at any time
- Master volume that follows the Windows volume bar and also controls your other devices
- Per device: volume up to 200%, mute, left/right balance and delay
- Sound tab: 10-band equalizer, presets, bass, treble and stereo width, for all devices
  together or for each device on its own, with an Original setting that changes nothing
- Keeps playing when a device disconnects, and brings it back when it returns
- Dark and light themes
- Remembers your devices, volumes, equalizer and theme between runs
- Groups: save the devices you play on together, play them in one click and control the group on its own
- Your own equalizer presets, saved by name
- Optional **Full control** mode (with a free virtual audio cable) so the equalizer and
  delay also work on the device that Windows uses by default

## Coming next

- 3D virtualizer and reverb effects
- System tray icon and start with Windows
- An installer and a portable download

## Try it

You need Windows 10 or 11, [Git](https://git-scm.com/) and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/abinaze/Audora.git
cd Audora
dotnet run --project src/Audora.App
```

Start some music, then turn on the devices you want to play on. A command-line tool for
developers is in `tools/Audora.Cli`. Having trouble? See [Troubleshooting](docs/TROUBLESHOOTING.md).

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

Free for personal and other noncommercial use under the [PolyForm Noncommercial License 1.0.0](LICENSE). Selling Audora, or using it in a paid product or service, needs permission from the author. Built with [NAudio](https://github.com/naudio/NAudio).
