# How Audora works

## The idea

Windows cannot send one app's sound to two devices by itself. Audora:

1. **Listens** to everything playing on the default output (WASAPI loopback capture).
2. **Copies** it to every other device you choose, each with its own volume, mute,
   left/right balance and delay.

The default device keeps playing directly. The others are delayed by their own buffers,
so Audora's per-device delay lines up devices that have different latency, such as
Bluetooth and wired.

## Parts

| Part | Job |
|---|---|
| `DeviceEnumerator` | Lists audio outputs, says how each is connected, reports plug and unplug |
| `MirrorEngine` | Captures the source and feeds each output |
| `OutputChannel` | One target device with its own volume, mute, balance and delay |
| `MirrorPlanner` | Decides which outputs to start or stop (pure logic, fully tested) |
| `MirrorController` | Keeps your selection playing when devices come and go or the default changes |
| `DefaultDeviceSwitcher` | Changes the Windows default output |
| `AudioRestore` | Puts the default output, its volume and its mute back as they were |
| `AudioScanner` and `ScanEvaluator` | The read-only scan: collects facts about Windows audio, then plain rules turn them into findings |
| `SoundCheckPlayer` | Plays a quiet test tone and reads the left and right level that Windows measures at the device |
| `UnmuteRepair` | The one repair so far: unmutes a device after you approve, checks the result and can undo it |

## The engine switch

Audora starts **off**. While it is off nothing runs: no capture, no outputs, no timers and no reaction to device
changes. `MirrorController.StartEngine` turns it on and `StopEngine` turns it off. Each time it is turned on,
Audora saves a restore point (the default output, its volume and its mute). **Restore Windows audio** puts those
back. It finds the device by its id first, then by its name, and asks before it changes anything.

## Volume

The master slider is the Windows volume of the default output, in both directions. A mirrored device plays at its
own slider (0 to 200%) times the master volume times its group volume. A muted master counts as zero.

## Health: scan, Sound Check and repairs

The Health page works with the engine off. It never changes a setting by itself.

- **Scan** (`AudioScanner`) reads the Windows audio services, outputs, microphones, driver status, sound format,
  the enhancements flag, the Windows Mono audio setting and the mute state. `ScanEvaluator` holds the plain rules
  that turn those facts into findings, so the rules can be tested without a sound card.
- **Sound Check** (`SoundCheckPlayer`) plays a quiet tone on one device at a time, in shared mode, with a fade in
  and a fade out. While it plays it reads the left and right peak level at the device. `ChannelCheck` turns the two
  levels into a verdict such as "matches", "mono mix" or "leaks to the other side". `SoundCheckGuard` refuses to
  play when the Windows volume is high.
- **Repairs** (`UnmuteRepair`) work on one device, ask first, check the result and can be undone. The Mono audio
  setting is only opened in Windows. Audora does not write it.

The details and the reasons are in [DIAGNOSTICS.md](DIAGNOSTICS.md).

## Folders

| Project | Purpose |
|---|---|
| `src/Audora.Core` | The engine, with no user interface |
| `src/Audora.App` | The desktop app (WPF) |
| `tests/Audora.Tests` | Unit tests |
| `tools/Audora.Cli` | Command-line tool for testing the engine |
| `tools/Audora.IconGen` | Builds the app icon from the logo |

## Known limits

- The default device itself cannot be delayed without a virtual audio driver. To line up
  a slow device, make it the default and delay the others.
- Bluetooth delay varies by device, so lining up is manual for now.
- Some copy-protected audio cannot be captured.
- When the Windows default output applies the Windows volume before Audora hears the sound, the volume can be applied twice on the other devices, so they are quieter than expected at a low master volume. Some outputs, such as a laptop's built-in speakers, do this. The USB device we tested does not. Volume calibration is planned.
