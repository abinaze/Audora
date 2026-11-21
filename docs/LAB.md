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
