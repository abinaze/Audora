# Troubleshooting

## My device is not in the list

- Turn the device on and, for Bluetooth, make sure Windows shows it as connected.
- In Windows Sound settings, check the device is **enabled**.
- Run `dotnet run --project tools/Audora.Cli -- list --all` to see every device Windows knows,
  including disconnected ones.

## There is no sound on one of the extra devices

- Make sure your music is playing on the Windows **default** output. Audora copies what the
  default device plays.
- Check that device's own volume in Windows. Audora's slider works on top of it.
- Run `dotnet run --project tools/Audora.Cli -- probe` while music plays. If the "captured" number
  grows, capture works and the problem is with the target device.

## The Bluetooth device sounds late (an echo)

Bluetooth always adds delay. Audora's default device plays instantly, so the others can
be behind. For the best result make your slowest device (usually Bluetooth) the Windows
default, then add delay to the faster devices until everything lines up.

## Other devices get too quiet when I lower the Windows volume

Audora gets the sound before the Windows volume and applies your master volume once. That is exact on the outputs
we measured. If the other devices drop much more than the default one, check their own sliders and their Windows
volume first. Then close Audora and run `dotnet run --project tools/Audora.Cli -- calibrate` with your default
output at 50% volume or less, and send us the `RESULT` line.

## Bluetooth headphones show up twice

Windows creates two entries for each Bluetooth headset: **Headphones** (stereo music, the
one you want) and **Headset ... Hands-Free** (phone-call mode, mono and low quality).
Choose the Headphones entry.

## "Capture stopped" or error 0x88890004

Windows reset the audio device, for example when it was unplugged or the default output
changed. Audora restarts capture and your devices by itself. If it keeps happening, run
`dotnet run --project tools/Audora.Cli -- list` and tell us what you see.

## Songs sound dull or cut out with noise removal on

Switch **Noise removal** off for that device. It keeps voices and removes the rest, so music suffers.
It is meant for calls and talk. See [Noise removal](NOISE.md).

## Information to include in a bug report

```bash
dotnet run --project tools/Audora.Cli -- list --all
dotnet run --project tools/Audora.Cli -- dump
```

Remove any device names you would rather not share before pasting.

## Where is the log file?

`%LOCALAPPDATA%\Audora\logs\audora.log` (paste that into the Windows Explorer address bar).
It holds device names and error messages. When you report a bug, attach the last 40 lines.

## My sound sounds wrong or stopped

Try these in order. Stop when it is fixed.

1. **Turn Audora off** with the switch above the master volume (or right-click the tray icon, **Turn Audora off**).
   If the sound is fine now, tell us what Audora was doing.
2. **Make sure no old copy is running.** Right-click the tray icon, **Quit Audora**. In Task Manager, look for
   `Audora` or `Audora.App`.
3. **Check Windows.** Settings → System → Sound: the right output is selected, its volume is up and it is not muted.
4. **Run the Windows fixer.** Settings → System → Sound → Troubleshoot common sound problems.
5. **Try without enhancements.** Sound settings → your device → Audio enhancements: switch them off and test.
6. **Restart the Windows audio services.** Open PowerShell as administrator and run:
   `Restart-Service Audiosrv -Force; Restart-Service AudioEndpointBuilder -Force`.
   Sound stops for a second and comes back.
7. **Restart the driver.** Device Manager → Sound, video and game controllers → right-click your device → Disable
   device, then Enable device. If it still fails, install the newest audio driver from your PC maker.
8. **Restore from Audora.** Settings → **Restore Windows audio** puts back the default output, volume and mute.

Software cannot repair a damaged speaker or a broken cable.

## Left and right play on both sides

Run Health, Scan now. If it reports "Windows Mono audio is on", turn it off in Settings, Accessibility, Audio.
Then run Sound Check on the device: left and right should measure on separate sides.

## A device plays no sound

Run Health, Scan now. A muted device is listed with an Unmute button. Sound Check also refuses to play on a
muted device and says so.
