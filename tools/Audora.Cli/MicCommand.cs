using Audora.Core.Audio;
using Audora.Core.Cleanup;
using Audora.Core.Devices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Audora.Cli;

/// <summary>
/// Listens to a microphone for a few seconds, first without and then with noise removal, and prints how loud the
/// room noise was each time. It only measures. No sound is saved, played or sent.
/// </summary>
internal static class MicCommand
{
    private const int PhaseSeconds = 5;

    public static int Run(DeviceEnumerator devices, string[] args)
    {
        var active = devices.GetCaptureDevices().Where(d => d.Status == DeviceStatus.Active).ToList();
        var term = string.Join(' ', args).Trim();
        var matches = term.Length == 0
            ? active.Where(d => d.IsDefault).ToList()
            : active.Where(d => d.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count != 1)
        {
            Console.WriteLine("Usage: Audora.Cli mic [part of microphone name]");
            Console.WriteLine(matches.Count == 0
                ? "No matching active microphone. Active microphones:"
                : $"\"{term}\" matches several microphones; be more specific:");
            foreach (var d in matches.Count == 0 ? active : matches)
            {
                Console.WriteLine($"  {d.Name}");
            }

            return 1;
        }

        try
        {
            return Measure(matches[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Windows could not open the microphone: {ex.Message}");
            Console.WriteLine("Check Settings > Privacy & security > Microphone.");
            return 1;
        }
    }

    private static int Measure(AudioDeviceInfo chosen)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDevice(chosen.Id);
        using var capture = new WasapiCapture(device);

        var format = capture.WaveFormat;
        if (format.BitsPerSample != 32)
        {
            Console.WriteLine($"This microphone's format ({format}) is not supported by this test.");
            return 1;
        }

        using var cleaner = new MicrophoneCleaner(format.SampleRate, format.Channels, () => new RnNoiseDenoiser());
        if (!cleaner.IsSupported)
        {
            Console.WriteLine(cleaner.UnavailableReason);
            return 1;
        }

        capture.DataAvailable += (_, e) =>
        {
            cleaner.Push(e.Buffer, e.BytesRecorded);
            cleaner.ReadAvailable();
        };

        Console.WriteLine($"Microphone: {chosen.Name} ({format.SampleRate} Hz, {format.Channels} channel(s))");
        Console.WriteLine($"Listening for {PhaseSeconds} seconds twice. Nothing is saved or played.");
        Console.WriteLine("Keep the room as it is, and do not talk.");
        Console.WriteLine();

        capture.StartRecording();
        try
        {
            var before = Listen(cleaner, "Noise removal OFF", PhaseSeconds);
            cleaner.Enabled = true;
            var after = Listen(cleaner, "Noise removal ON", PhaseSeconds);
            Summarize(cleaner, before, after);
        }
        finally
        {
            capture.StopRecording();
        }

        return 0;
    }

    private static List<MicrophoneLevels> Listen(MicrophoneCleaner cleaner, string label, int seconds)
    {
        Console.WriteLine(label);
        cleaner.TakeLevels();
        var rows = new List<MicrophoneLevels>();
        for (var second = 1; second <= seconds; second++)
        {
            Thread.Sleep(1000);
            var levels = cleaner.TakeLevels();
            rows.Add(levels);
            Console.WriteLine($"  {second}s  in {Db(levels.InputRms),6:0.0} dB   out {Db(levels.OutputRms),6:0.0} dB   peak in {levels.InputPeak:0.000}");
        }

        return rows;
    }

    private static void Summarize(MicrophoneCleaner cleaner, List<MicrophoneLevels> before, List<MicrophoneLevels> after)
    {
        var peak = before.Count == 0 ? 0f : before.Max(l => l.InputPeak);
        Console.WriteLine();
        Console.WriteLine(MicrophoneCheck.Describe(MicrophoneCheck.Judge(peak)));

        if (cleaner.Failed)
        {
            Console.WriteLine($"Noise removal stopped: {cleaner.UnavailableReason}");
            return;
        }

        var offDb = Db(MeanRms(before.Select(l => l.OutputRms)));
        var onDb = Db(MeanRms(after.Select(l => l.OutputRms)));
        Console.WriteLine($"Room noise without noise removal: {offDb:0.0} dB. With it: {onDb:0.0} dB. Change: {onDb - offDb:0.0} dB.");
        Console.WriteLine("About -10 dB sounds roughly half as loud. Voices are kept, so this only describes steady room noise.");
    }

    private static float MeanRms(IEnumerable<float> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0f : (float)Math.Sqrt(list.Average(v => (double)v * v));
    }

    private static double Db(float rms) => 20.0 * Math.Log10(Math.Max(rms, 1e-6f));
}
