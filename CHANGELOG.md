# Changelog

All notable changes are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Audora engine on/off switch (starts off by default), Restore Windows audio, and a safer start with Windows that refuses development builds
- Read-only audio scan (Health page and command line): checks the Windows audio services, sound outputs, microphones, driver status and sound format, and changes nothing
- Sound Check test tones (Health page and command line): left, right, both and a sweep, quiet and stoppable at once
- Repairs, first step: the scan can unmute a muted device and open the Windows Mono audio setting. Each asks first, checks the result and can be undone
- Sound Check now shows the left and right levels Windows measured at the device, with a plain verdict
- The scan notes when Windows Mono audio is on, and Sound Check names the devices a tone is copied to and can turn Audora off for testing
- The log now records when Audora turns on or off, every 15 seconds whether it hears sound while on, and what Sound Check does

## [0.1.0] - 2026-11-08

### Added
- Detection of every Windows audio output, with connection type (Bluetooth, USB, HDMI, wired) and live plug and unplug events
- Audio engine that plays system audio on several devices at once
- Per-device volume, mute, left/right balance and delay
- Automatic recovery when a device disconnects, returns, or the Windows default changes
- Command-line tool for trying and debugging the engine
- Audora logo, brand files and Windows app icon
- Project documentation, contribution guide, issue forms and security policy
- Desktop app with a card per device, master volume linked to the Windows volume bar, volume boost to 200%
- Sound tab with a 10-band equalizer, presets, bass, treble and stereo width for all devices or each device
- Optional Full control mode using a virtual audio cable
- Sound profiles (Music, Movie, Communication, Gaming, Smart) with voice clarity, a volume stabilizer and a 3D surround effect
- Connect and disconnect paired Bluetooth audio devices from the Devices tab
- System tray icon with quick controls, keep running in the background, optional start with Windows
- Windows installer, portable download, automatic release build and a project website
- Saved settings, saved devices, device groups and your own equalizer presets

### Changed
- License changed from MIT to PolyForm Noncommercial 1.0.0: free for personal and noncommercial use, commercial use needs permission
