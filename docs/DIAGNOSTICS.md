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

## From the research

Two outside research reports were checked against what Audora does and what it must never do.

### Adopted

- **Level meter at the device.** Windows can report the peak level of each channel at a device. Sound Check uses it
  to show what Windows measured on the left and right while a tone plays.
- **Microphone privacy.** When Windows blocks microphone access it still opens the stream but feeds zeros. An all-zero
  recording is reported as "possibly blocked by Windows privacy", never as a broken microphone.
- **Apps using a device.** A read-only list of which apps have a sound stream open on each device.
- **Recent audio errors.** A read-only look at recent audio crashes and driver errors in the Windows event log.
  The package to read the log is checked before use.
- **Bluetooth call mode.** A note that using the microphone can make Windows switch headphones to the
  lower-quality call profile. Audora only explains it.
- **Repair order, least to most disruptive:** correct a format, restore the default device, change one device's
  enhancements, restart one device's driver, restart the Windows audio service last (it cuts all sound).
- **Every repair:** a snapshot first, your approval, one device at a time, a re-check afterwards, and a way back.
- **Administrator rights** are asked for only at the moment one action needs them: restarting a driver, changing
  registry settings under HKLM, and restarting a Windows service. Playing tones, reading levels, listing devices and
  switching the default device need none.
- **Honest wording at the hardware boundary:** "The software and driver stack sent the signal, but the sound that
  comes out cannot be verified."

### Not adopted

- Disabling a Bluetooth headset profile automatically. It can switch the headset microphone off for good.
- Event tracing (ETW) for DPC and interrupt delays. It needs administrator rights and heavy parsing. For crackle from
  system delays, the free LatencyMon tool is the pointer.
- Proving a speaker or microphone is healthy from a digital level or spectrum alone. The signal is read before the
  speaker, so it cannot see hardware faults.
- Repair recipes that could not be verified, for example a global registry flag that "disables enhancements".
  Each repair is read, written and rolled back on a real device first.

### Possible later

- An optional **Acoustic check:** play a tone and listen with the microphone for 3 seconds, saving nothing. This is the
  only way software can check what a speaker actually produces.
- Per-app volume from the list of apps using a device.

## Repairs built so far

- **Unmute a device.** Found by the scan. Asks first, turns off the Windows mute of that one device, checks what
  Windows reports afterwards, re-scans, and offers an Undo. No administrator rights.
- **Open the Mono audio setting.** Opens the Windows page. Audora does not change that setting for you.

Not built yet: restart one device's driver, change a device's enhancements, restore the default device, restart the
Windows audio service. Those need a snapshot, approval, and administrator rights at the moment they run.

## Verified findings

- **Windows volume and loopback.** Loopback audio is taken before the endpoint volume and mute by default, but the
  tap point depends on the driver. On the test PC the USB headphones' loopback ignored the Windows volume, and the
  Realtek speakers' loopback followed it. When the default device is the Realtek output, the master volume is
  therefore applied twice on mirrored devices. A per-device calibration is planned.
- **Windows Mono audio** is stored as AccessibilityMonoMixState under HKCU\Software\Microsoft\Multimedia\Audio.
  The scan reads it. Its signature: a one-sided tone measures half on both sides in the engine's capture while the
  device meter shows one side only.
- **Muted devices.** Audora's Mute button mutes the default device. If the default changes, Audora now unmutes the
  device it muted.
- **Event log.** On .NET 8 the reader comes from the System.Diagnostics.EventLog package. Whether a standard user
  can read the System log has to be tested before that check is built.
