# How Audora treats your Windows audio

Audora is meant to sit on top of Windows audio and stay out of the way. This page says exactly what it can change.

## When Audora is off

Nothing. It does not listen, copy, watch devices, check Bluetooth or change any setting. Windows audio works as
usual. Audora starts **off** every time unless you choose "Turn Audora on when it starts" in Settings.

## When Audora is on

It plays your sound on the devices you switched on. It changes Windows audio only when you ask:

| Action | What changes in Windows |
|---|---|
| Move the master volume or press Mute | The volume or mute of your default output |
| Mute all / Unmute all | The mute of the default output |
| Make Windows default, or Full control | The default output (not the calls device) |
| Connect or Disconnect a Bluetooth device | That device's audio connection |
| Start with Windows | One entry in your own Windows startup list |
| Fix: Unmute (Health page, after you approve it) | The Windows mute of that one device |

Audora never changes sample rates or sound enhancements, never takes exclusive control of a device, never
disables a device, and never installs drivers.

## Health page and Sound Check

The scan only reads. It changes nothing, and it works with Audora turned off.

Sound Check plays a quiet tone and never changes the Windows volume. If the Windows volume is high it refuses to
play: above 50% you can choose **Play anyway**, and above 80% it never plays. It shows what Windows measured at
the device and asks what you heard. The answers stay in the window and are not saved.

A Fix button asks first, changes one thing on one device, checks the result and has an **Undo**. **Unmute** changes
the mute of that device. **Open Settings** only opens the Windows Mono audio page. Audora does not change that setting.

If you press Mute in Audora and Windows then changes the default output, Audora unmutes the old device it muted,
so nothing stays muted behind your back.

## Put things back

Settings → **Restore Windows audio** turns Audora off and puts back the default output, its volume and mute as
they were when you last turned Audora on. It asks first.

## Start with Windows

It works only from the installed or portable version. A build that runs from a source folder is refused, and
Audora removes a leftover startup entry that points at one.

## If your sound is wrong

See [Troubleshooting](TROUBLESHOOTING.md#my-sound-sounds-wrong-or-stopped). Turn Audora off first. If the problem
stays, it is in Windows or the driver, not in Audora.
