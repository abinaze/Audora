using System.Diagnostics;
using System.Globalization;
using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Cli;

/// <summary>
/// Measures where Windows takes the loopback sound of the default output from: before or after the Windows
/// volume. It plays one quiet test tone on the default output and compares what loopback hears with what
/// Windows measures at the device. It changes nothing, and it never changes the Windows volume.
/// Run it at a few Windows volumes below 80% and keep the RESULT lines.
/// </summary>
internal static class CalibrateCommand
{
    private const double WindowStart = 0.9; // seconds after the tone starts, once the fade in is over
    private const double WindowEnd = 2.4;
    private const float QuietBelow = 0.002f;

    public static int Run(DeviceEnumerator devices, bool allowLoud)
    {
        if (Process.GetProcessesByName("Audora").Length > 0)
        {
            Console.WriteLine("Audora is running. Close it first, so the test tone is not copied to your other devices.");
            return 1;
        }

        var target = devices.GetActiveRenderDevices().FirstOrDefault(d => d.IsDefault);
        if (target is null)
        {
            Console.WriteLine("There is no active default output.");
            return 1;
        }

        using var player = new SoundCheckPlayer();
        var state = player.ReadWindowsVolume(target.Id);

        switch (SoundCheckGuard.Decide(state, allowLoud))
        {
            case GuardDecision.Muted:
                Console.WriteLine($"{target.Name} is muted in Windows. Unmute it and try again.");
                return 1;
            case GuardDecision.TooLoud:
                Console.WriteLine($"The Windows volume of {target.Name} is above 80%. Lower it first. A volume of 20% to 60% gives the best result.");
                return 1;
            case GuardDecision.Loud:
                Console.WriteLine($"The Windows volume of {target.Name} is above 50%. Lower it to 50% or less, or add --ok to play anyway. Audora never changes your volume.");
                return 1;
        }

        using var meter = new LoopbackPeakMeter();
        try
        {
            meter.Start(target.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not listen to {target.Name}: {ex.Message}");
            return 1;
        }

        Thread.Sleep(1000);
        var quiet = meter.TakePeak();
        if (quiet > QuietBelow)
        {
            Console.WriteLine($"Other sound is playing on {target.Name} (peak {Show(quiet)}). Pause it and run again.");
            return 1;
        }

        var done = new ManualResetEventSlim();
        player.Finished += (_, _) => done.Set();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            player.Stop();
            done.Set();
        };

        var request = new ToneRequest(ToneChannel.Both, ToneShape.Steady, 440, 3);
        Console.WriteLine($"Playing a quiet test tone on {target.Name} for 3 seconds. Press Ctrl+C to stop at once.");
        player.Play(target.Id, request);

        var clock = Stopwatch.StartNew();
        var loopback = 0f;
        var measured = 0f;
        while (clock.Elapsed.TotalSeconds < request.Seconds && !done.IsSet)
        {
            Thread.Sleep(40);
            var heard = meter.TakePeak();
            var peaks = player.ReadChannelPeaks();
            var t = clock.Elapsed.TotalSeconds;
            if (t >= WindowStart && t <= WindowEnd)
            {
                loopback = Math.Max(loopback, heard);
                if (peaks is not null && peaks.Length > 0)
                {
                    measured = Math.Max(measured, peaks.Max());
                }
            }
        }

        player.Stop();

        if (loopback < QuietBelow)
        {
            Console.WriteLine("Loopback heard no tone, so nothing could be measured. Try again.");
            return 1;
        }

        var tone = ToneSampleProvider.DefaultLevel;
        var volumeText = state is null ? "unknown" : $"{state.Volume * 100:0}%";

        Console.WriteLine();
        Console.WriteLine($"Device         : {target.Name}");
        Console.WriteLine($"Windows volume : {volumeText}");
        Console.WriteLine($"Tone level     : {Show(tone)}");
        Console.WriteLine($"Loopback peak  : {Show(loopback)}   (ratio to the tone {Show(loopback / tone)})");
        Console.WriteLine($"Meter peak     : {Show(measured)}   (ratio to the tone {Show(measured / tone)})");
        Console.WriteLine();
        Console.WriteLine($"RESULT device=\"{target.Name}\" volume={Show(state?.Volume ?? -1f)} tone={Show(tone)} loopback={Show(loopback)} meter={Show(measured)}");
        return 0;
    }

    private static string Show(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);
}
