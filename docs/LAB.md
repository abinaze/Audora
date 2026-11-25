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

## Reading a native library

`lab exports <library.dll>` lists the function names a native library offers by reading its file. It does not load or
run anything, so it is safe to use on a library you have not decided to trust yet. It is how we check what a library
really offers before writing code against it.

## Results so far

Voice Bank + DEMAND test set (University of Edinburgh, 48 kHz), 60 clips spread across the set, 145 seconds in total,
measured on one PC. Clarity gain is the change in SI-SDR against the clean recording (higher is better). Clean speech
kept is the SI-SDR of the engine's output on the clean clips (higher is better).

| Engine | Clarity gain | Clean speech kept |
|---|---|---|
| RNNoise, strength 100% | +4.6 dB | 15.9 dB |
| RNNoise, strength 85% | +5.2 dB | 17.3 dB |
| RNNoise, strength 70% | +4.9 dB | 19.1 dB |
| RNNoise, strength 55% | +4.1 dB | 21.2 dB |
| RNNoise, strength 40% | +3.1 dB | 24.0 dB |
| DeepFilterNet command-line tool 0.5.4, default model, run offline | +10.2 dB | 39.4 dB |

What this does not show: only 60 clips; SI-SDR is one measure and not a listening test; DeepFilterNet was run on files, so
its delay and processor use in live use were not measured; and either model may have seen similar speech or noise in its
training, so the margin on other noises may be smaller.

## Trying the DeepFilterNet library in the benchmark

The benchmark can also run DeepFilterNet3 through its native library. Put `deepfilter.dll` and
`DeepFilterNet3_onnx.tar.gz` in one folder, set `AUDORA_DFN_DIR` to that folder, and run `lab bench` or `lab bench-set`.
A row for DeepFilterNet3 then appears next to the RNNoise rows. This is for testing only: the app does not use it yet, and
Audora does not ship these files yet.
