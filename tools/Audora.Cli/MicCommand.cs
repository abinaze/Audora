using Audora.Core.Audio;
using Audora.Core.Cleanup;
using Audora.Core.Devices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Audora.Cli;

/// <summary>
/// Listens to a microphone with noise removal on, once while you stay quiet and once while you talk, and prints how
/// much the cleaning changed the loudness each time. Every line compares the same sound before and after cleaning.
/// It only measures. No sound is saved, played or sent.
/// </summary>
internal static class MicCommand
{
    private const int PhaseSeconds = 5;
    private const double QuietRoomDb = -60.0;

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

        cleaner.Enabled = true;
        if (cleaner.Failed)
        {
            Console.WriteLine($"Noise removal could not start: {cleaner.UnavailableReason}");
            return 1;
        }

        capture.DataAvailable += (_, e) =>
        {
            cleaner.Push(e.Buffer, e.BytesRecorded);
            cleaner.ReadAvailable();
        };

        Console.WriteLine($"Microphone: {chosen.Name} ({format.SampleRate} Hz, {format.Channels} channel(s))");
        Console.WriteLine("Noise removal is ON. Each line compares the same sound before (in) and after (out) cleaning.");
        Console.WriteLine("Nothing is saved or played. For a fair test, run a fan or play steady noise nearby.");
        Console.WriteLine();

        capture.StartRecording();
        try
        {
            var quiet = Listen(cleaner, "Part 1: stay quiet, only the room noise", PhaseSeconds);
            var talk = Listen(cleaner, "Part 2: talk normally", PhaseSeconds);
            Summarize(cleaner, quiet, talk);
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
            var change = Db(levels.OutputRms) - Db(levels.InputRms);
            Console.WriteLine($"  {second}s  in {Db(levels.InputRms),6:0.0} dB   out {Db(levels.OutputRms),6:0.0} dB   change {change,5:0.0} dB");
        }

        return rows;
    }

    private static void Summarize(MicrophoneCleaner cleaner, List<MicrophoneLevels> quiet, List<MicrophoneLevels> talk)
    {
        var peak = quiet.Concat(talk).Select(l => l.InputPeak).DefaultIfEmpty(0f).Max();
        Console.WriteLine();
        Console.WriteLine(MicrophoneCheck.Describe(MicrophoneCheck.Judge(peak)));

        if (cleaner.Failed)
        {
            Console.WriteLine($"Noise removal stopped: {cleaner.UnavailableReason}");
            return;
        }

        if (Db(MeanRms(quiet.Select(l => l.InputRms))) < QuietRoomDb)
        {
            Console.WriteLine("The room was almost silent, so the room noise number is not a real test. Run a fan or play steady noise near the microphone and try again.");
        }

        Console.WriteLine($"Room noise changed by {Change(quiet):0.0} dB. Your voice changed by {Change(talk):0.0} dB.");
        Console.WriteLine("It works well when the room noise goes clearly below 0 dB and the voice stays close to 0 dB.");
        Console.WriteLine("About -10 dB sounds roughly half as loud. A very quiet room leaves little to remove.");
    }

    private static double Change(List<MicrophoneLevels> rows) =>
        Db(MeanRms(rows.Select(l => l.OutputRms))) - Db(MeanRms(rows.Select(l => l.InputRms)));

    private static float MeanRms(IEnumerable<float> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0f : (float)Math.Sqrt(list.Average(v => (double)v * v));
    }

    private static double Db(float rms) => 20.0 * Math.Log10(Math.Max(rms, 1e-6f));
}
