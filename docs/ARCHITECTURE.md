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
