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

Audora never changes sample rates or sound enhancements, never takes exclusive control of a device, never
disables a device, and never installs drivers.

## Put things back

Settings → **Restore Windows audio** turns Audora off and puts back the default output, its volume and mute as
they were when you last turned Audora on. It asks first.

## Start with Windows

It works only from the installed or portable version. A build that runs from a source folder is refused, and
Audora removes a leftover startup entry that points at one.

## If your sound is wrong

See [Troubleshooting](TROUBLESHOOTING.md#my-sound-sounds-wrong-or-stopped). Turn Audora off first. If the problem
stays, it is in Windows or the driver, not in Audora.
