# Audora architecture

## Approach

Windows cannot route one app's audio to several devices by itself, so Audora:

1. Captures the default output with WASAPI loopback.
2. Re-renders the stream to every additional selected device, each with its own
   gain, balance and delay buffer.

The default Windows device keeps playing natively. Additional devices are
delayed by their own buffers, so per-device delay is used to line up devices
with different latency (for example Bluetooth versus wired).

## Projects

| Project | Purpose |
|---|---|
| `src/Audora.Core` | Device detection, audio engine, settings (no UI) |
| `src/Audora.App` | WPF user interface, tray, themes |
| `tests/Audora.Tests` | Unit tests for Core |

## Known limitations

- Delaying the default device itself would need a virtual audio driver (not in v1).
- Bluetooth latency varies by device; sync is manual per-device delay at first.
- Some DRM-protected audio cannot be captured.
