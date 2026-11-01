# Audora

**Universal audio control center for Windows.**

Connect multiple audio devices, select any combination, play system audio
through all of them at once, and control each device independently.

> Status: early development. Not yet usable.

## Planned features

- Detect Bluetooth, USB, wired (3.5 mm) and HDMI/DisplayPort outputs
- Play the same audio through multiple devices simultaneously
- Per-device volume, mute, balance and delay
- Master controls and saved device configurations
- Dark/light themes, system tray, optional startup with Windows
- Local only: no account, no cloud, no telemetry

## Links

- Author: [abinaze](https://github.com/abinaze)
- License: MIT

## Building from source

Requires Windows 10/11 and the .NET 8 SDK.

    git clone https://github.com/abinaze/Audora.git
    cd Audora
    bash scripts/validate.sh
    dotnet run --project src/Audora.App

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the design.
