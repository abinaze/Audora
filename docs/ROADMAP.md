# Roadmap

What Audora does, what is next, and what we decided not to do. Ideas come from similar apps
(Nahimic, EarTrumpet, FxSound, Voicemeeter, Sonos groups) and from the Windows 11 Shared Audio feature.

## Done

- Play on any number of devices at once, of any mix (Bluetooth, USB, wired, HDMI)
- Master volume tied to the Windows volume bar, per-device volume up to 200%, mute, balance, delay
- Sound profiles: Music, Movie, Communication, Gaming and Smart (picks one for the app you are using)
- Effects: bass, treble, voice clarity, volume stabilizer, 3D surround, stereo width, 10-band equalizer
- Your own saved presets, per device or for all devices
- Groups of devices with their own volume and mute
- Connect and disconnect paired Bluetooth devices from inside Audora
- Tray icon with quick controls, runs in the background, optional start with Windows
- Installer, portable download, release build and website (ready; first release pending)
- Remembers devices, volumes, sound and theme
- Optional Full control mode with a virtual audio cable

## Next

- Diagnostics and Repair Center: scan, Sound Check (test tones, microphone level) and targeted repairs with backup and rollback
- More read-only checks: apps using each device, recent Windows audio errors, Bluetooth call mode, microphone privacy

- First public release, a winget package and a code-signed installer
- Noise removal for the microphone (no driver needed), then a clean microphone for other apps through a virtual cable you install, then optional voice cleanup for call audio
- Per-app volume (like EarTrumpet)

## Later, if there is demand

- Choose which app each profile belongs to in Smart mode
- Reverb (the engine is ready, the controls are hidden for now)

## Not planned

- In-game sound radar overlays: they need integration with each game.
- Anything that needs an account, a server or telemetry.

## Good to know

- Nahimic's Sound Sharing and Windows 11 Shared Audio work with two Bluetooth devices. Audora has no such limit.
  How many Bluetooth audio devices play at once is decided by your PC's Bluetooth adapter, usually two to four.
- Windows plays the default device directly. Audora cannot change its sound unless Full control is on.
- Smart mode looks at the name and title of the app in front, on your PC only. Nothing is stored or sent.
