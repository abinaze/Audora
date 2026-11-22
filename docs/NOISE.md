# Noise removal

Plain answers about what Audora's noise removal does, and what it does not.

## What it is

Noise removal cleans the **voices** in the sound a device plays. It uses RNNoise, a small model that keeps
speech and treats everything else as noise. It runs on your PC. Nothing is saved or sent.

- It is **off** by default. You switch it on for one device at a time, on the device card under Fine tune.
- Strength mixes the cleaned sound with the original. Lower it if voices sound thin.
- Use it for calls, talks and lectures that have a steady hum or fan under the voice.

## What it is not

It is **not** noise cancelling like AirPods or noise-cancelling headphones, and Audora does not claim to be.

Those headphones cancel the sound around you before it reaches your ear. They need a microphone at the ear and a
chip that answers in a fraction of a millisecond. A PC and ordinary headphones cannot do that:

- A laptop microphone is far from your ear, so it hears something different from what your ear hears.
- Bluetooth adds well over 100 milliseconds. Wired sound on Windows adds several milliseconds at best.
  The cancelling sound has to arrive within about a millisecond or two, or it makes the noise worse.
- The research we read points to a narrow low hum at best, and only with special set-ups. We found no proof of
  it working on an ordinary PC, so Audora does not offer it.

If your headphones have their own noise cancelling, use their button or app. Windows has no switch for it.

## Only during calls

By default, noise removal on a device works only while a call app is in front (Teams, Zoom, Meet, Discord, Skype,
Webex, Slack, WhatsApp or Telegram). The rest of the time the sound plays untouched, so music is never changed.
The device card says "Waiting" or "Cleaning now". Switch **Only during calls** off to clean all the time, but music
will sound dull.

To tell which app is in front, Audora reads its name and window title on this computer, as Smart mode does.
Nothing is stored or sent. Cleaning starts or stops after two checks in a row agree, about 4 seconds, so a quick
glance at another window does not change it.

## Music

With noise removal on, music can sound dull, parts can cut out and the sound can get quieter. This is how RNNoise
works: it keeps speech and removes the rest, and music is not speech. Keep it **off** for songs.

## Good to know

- A cleaned device plays about 20 ms later. Use the Delay setting to line it up with the others.
- A device whose sound is not 48 kHz gets a short gap when you switch noise removal on or off.
- Surround sound (more than two channels) is not supported.
- The default Windows device plays directly, so Audora cannot clean it.
- If the cleaner fails for any reason, it switches itself off and the sound passes through untouched.

## Microphone

The Microphone page tries noise removal on a microphone. Pick one, switch noise removal on and press Test. It
listens for 15 seconds, shows the level before and after cleaning, and says how much quieter the sound became.
Nothing listens until you press Test. The sound is only measured. It is never saved, played or sent.

In our tests with a USB microphone, speech passed through within about 0.2 dB, while hiss and some sudden
non-speech sounds dropped by 13 to 37 dB. Background talk or music may show little change, because the model
treats them like speech.

If the meter stays empty, the microphone may be muted in Windows or blocked in Settings, Privacy and security,
Microphone. Audora does not change either.

## Planned

Sending the cleaned voice to other apps needs a virtual cable that you install yourself. It is planned and is not built yet.
