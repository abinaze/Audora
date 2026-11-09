# Diagnostics and Repair (plan)

Audora is growing from a volume tool into an audio control, testing and repair tool.
Everything here runs on your PC. Nothing is uploaded and no account is needed.

## The four stages

1. **Scan.** Finds what is wrong. Changes nothing.
2. **Sound Check.** Plays test tones and measures the result.
3. **Repair.** Offers a fix only for a problem that Scan or Sound Check found. You approve each one.
4. **Verify.** Runs the failed checks again and shows before and after.

Quick Scan takes seconds (services and devices). Deep Scan adds drivers, sample rate,
enhancements and the Bluetooth profile.

## Rules

- Sound Check works without the Repair Center.
- You can test one device or all devices.
- Tones are made on your PC. No internet is needed.
- A Stop button ends any test at once. Tests start quiet and never jump to a loud level.
- Tests use shared mode only, never exclusive mode.
- The microphone test never records or saves audio.
- Results are real measurements, not a guessed health score.
- Windows audio keeps working when the Audora engine is OFF.
- A scan never changes a setting.
- Every repair asks first, saves a snapshot, can be rolled back, and is checked afterwards.
- Administrator rights are asked for only at the moment one action needs them.
- Audora never uninstalls a driver on its own and never resets unrelated settings.

## Limits

Software cannot repair a damaged speaker, a broken cable, a faulty USB port or defective
Bluetooth hardware. Audora says "problem found" for a detection and "repair confirmed" only
after a check passes again.

## Stages of work

- **A. Scan.** A1 (done first): Windows audio services, outputs, microphones.
  A2: driver problems, sample rate, enhancements, Bluetooth profile.
- **B. Sound Check.** Left, right, stereo, mono, sweep, volume steps, latency, distortion,
  noise, loopback, microphone level. Needs microphone input support.
- **C. Repair Center.** Restart audio services, rescan hardware, re-enable a disabled device,
  restore the default devices, turn off a problem enhancement, repair Windows audio components,
  reinstall a chosen driver (with confirmation).
- **D. History and Recovery.** Local history with before and after, and a full recovery screen.
  The Restore Windows audio button already exists in Settings.

Audora has one device-detection engine. Diagnostics and audio control both use it.
