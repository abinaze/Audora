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

## Bluetooth headphones show up twice

Windows creates two entries for each Bluetooth headset: **Headphones** (stereo music, the
one you want) and **Headset ... Hands-Free** (phone-call mode, mono and low quality).
Choose the Headphones entry.

## "Capture stopped" or error 0x88890004

Windows reset the audio device, for example when it was unplugged or the default output
changed. Audora restarts capture and your devices by itself. If it keeps happening, run
`dotnet run --project tools/Audora.Cli -- list` and tell us what you see.

## Information to include in a bug report

```bash
dotnet run --project tools/Audora.Cli -- list --all
dotnet run --project tools/Audora.Cli -- dump
```

Remove any device names you would rather not share before pasting.
