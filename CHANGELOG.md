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
- Third-party software notices (THIRD-PARTY-NOTICES.md). The licence and the notices are now inside the installer and the portable zip
- A `calibrate` command in the command-line tool. It measures whether Windows gives Audora the sound before or after the Windows volume
- A `mic` command in the command-line tool. It listens to a microphone with noise removal on, once while you stay quiet and once while you talk, and prints how much the cleaning changed the room noise and your voice. It saves and plays nothing
- `lab record` and `lab bench` commands in the command-line tool. They save a short clip from a microphone to a file you name, and compare noise removal on your own voice and noise clips with numbers. They write only the file you ask for and send nothing. See docs/LAB.md
- A Microphone page: pick a microphone, switch noise removal on and press Test to see the level before and after cleaning. Nothing listens until you press Test, and no sound is saved, played or sent. The choices are remembered per microphone
- Noise removal for the sound a device plays: off by default, set per device, with a Strength slider. It is for calls and talk, not music. See docs/NOISE.md
- Noise removal on a device now works only while a call app is in front, so music is never touched. The device card says Waiting or Cleaning now. A new Only during calls switch turns this off. It is on by default, also for devices saved before

### Changed
- Clearer, shorter wording across the app
- The log records level checks only when something changes, and once every five minutes otherwise
- Dependabot leaves major versions of System packages alone and groups the rest
- The release script only accepts a new, higher version number, so a released version is never replaced
- Dependabot waits on NAudio minor updates until the engine is checked against them
- The website lists every feature, has a safety section, more questions, sharing tags, a 404 page and a sitemap, and the build checks the pages

### Fixed
- A device muted with the Mute button is unmuted when the Windows default device changes
- The scan describes a muted microphone as not recording
- Quitting Audora gives back the Windows sound that its Mute button muted, and the log now says when Audora mutes or unmutes
- Quitting Audora gives back the Windows mute first, before the slower shutdown steps, and the log says when Audora is closing

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
