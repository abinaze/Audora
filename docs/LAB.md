# Lab commands

Test tools in the command-line tool for comparing noise removal with numbers. They are for testing. The app does not
use them.

    dotnet run --project tools/Audora.Cli -- lab record 15 lab-voice.wav "microphone name"
    dotnet run --project tools/Audora.Cli -- lab record 15 lab-noise.wav "microphone name"
    dotnet run --project tools/Audora.Cli -- lab bench lab-voice.wav lab-noise.wav

- **record** saves a short clip (1 to 60 seconds) from a microphone to the file you name. Record your voice in a quiet
  room, then the noise you want to remove, without talking.
- **bench** mixes your noise into your voice at three levels and runs each engine. For each it prints the clarity before
  and after, how much the voice alone changed, how much quieter the noise alone became, and the delay.

The files hold what your microphone heard. Audora does not send them anywhere, and `*.wav` files are ignored by Git.
Delete them when you are done.

## Testing with a public dataset

Your own recordings are only a rough check. For a fair test, use clean speech and the same speech with noise added.
A well known set is the Voice Bank + DEMAND test set from the University of Edinburgh (48 kHz, Creative Commons
Attribution 4.0 licence): Valentini-Botinhao, C. (2017), "Noisy speech database for training speech enhancement
algorithms and TTS models", DOI 10.7488/ds/2117. Download the clean test set and the noisy test set from that page,
unpack each into its own folder, then run:

    dotnet run --project tools/Audora.Cli -- lab bench-set <clean folder> <noisy folder> 60

The command pairs clips by file name, runs a spread of up to 60 clips, and prints the average clarity before and after
cleaning, how well the engine keeps clean speech, the delay and the speed. Keep the dataset outside the repository.

## Comparing another engine

`lab subset` copies a spread of matching clean and noisy clips into a small folder, so a slower tool can be run on just
those clips. `lab compare-set` then scores sound that another program has already cleaned, in the same way as
`lab bench-set`. For example, with the DeepFilterNet command-line tool (published by its authors on GitHub, under an
open licence; check the licence and download only from the official release page):

    dotnet run --project tools/Audora.Cli -- lab subset <clean folder> <noisy folder> <subset folder> 60
    dotnet run --project tools/Audora.Cli -- lab bench-set <subset folder>/clean <subset folder>/noisy
    deep-filter -D -o enhanced-noisy <subset folder>/noisy/*.wav
    deep-filter -D -o enhanced-clean <subset folder>/clean/*.wav
    dotnet run --project tools/Audora.Cli -- lab compare-set <subset folder>/clean <subset folder>/noisy enhanced-noisy enhanced-clean

Both commands print the same kind of numbers, so the engines can be compared side by side.
