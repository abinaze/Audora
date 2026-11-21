# Privacy

Audora does not collect, store or send your personal data.

- No accounts, no telemetry, no analytics, no ads.
- Audio is read from your default output in memory, only to play it on your other devices.
  It is never recorded to disk or sent over the network.
- Audora does not use the internet.
- Audora keeps a small log file on your computer (`%LOCALAPPDATA%\Audora\logs`) with device
  names and error messages, to help fix problems. It is never sent anywhere, and you can
  delete it at any time.

If this ever changes, this page will change first.

## Smart sound mode

If you turn on the Smart profile, Audora reads the name and window title of the app in front of you,
and whether a fullscreen game is running, to choose between Music, Movie, Communication and Gaming.
This happens on your computer only. Nothing is stored or sent anywhere.

## Bluetooth

Audora asks Windows for your paired Bluetooth audio devices so you can connect them. The list stays on your
computer.

## Start with Windows

If you turn this on, Audora adds one entry to the Windows startup list of your own user account
(a registry value under HKEY_CURRENT_USER). Turning it off removes it. Nothing else is changed.

## Health scan

The Health page reads the state of the Windows audio services, the list of sound outputs and microphones,
and each connected device's driver status and sound format. It only reads, it changes nothing, and the
result stays on your computer.

## Noise removal

When you switch noise removal on for a device, the sound for that device is cleaned in memory on your computer.
Nothing is recorded to disk or sent anywhere. It does not use the internet.

The Microphone page listens to a microphone only while you press Test, for up to 15 seconds. It measures how loud
the sound is and shows it. Nothing is recorded, saved, played or sent.

Noise removal on a device works only while a call app is in front, unless you switch Only during calls off. To tell,
Audora reads the name and window title of the app in front of you, on your computer, as Smart mode does. Nothing is
stored or sent.

## Lab commands

The lab commands in the command-line tool write a file only when you run `lab record`, and only the file you name. It
holds what your microphone heard and stays on your computer. Delete it when you are done. See [Lab commands](docs/LAB.md).

## Sound Check

Sound Check plays short test tones that are made on your computer. It does not record anything.
The answers you give are shown on screen for the current session only and are not saved.
