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

## Music

With noise removal on, music can sound dull, parts can cut out and the sound can get quieter. This is how RNNoise
works: it keeps speech and removes the rest, and music is not speech. Keep it **off** for songs.

## Good to know

- A cleaned device plays about 10 ms later. Use the Delay setting to line it up with the others.
- A device whose sound is not 48 kHz gets a short gap when you switch noise removal on or off.
- Surround sound (more than two channels) is not supported.
- The default Windows device plays directly, so Audora cannot clean it.
- If the cleaner fails for any reason, it switches itself off and the sound passes through untouched.

## Planned

Noise removal for the microphone is planned. It is not built yet.
