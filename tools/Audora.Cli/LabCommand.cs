using System.Diagnostics;
using Audora.Core.Audio;
using Audora.Core.Cleanup;
using Audora.Core.Devices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Audora.Cli;

/// <summary>
/// Test tools for comparing noise removal engines with numbers. "record" saves a short clip from a microphone to a file
/// you name. "bench" mixes your own noise into your own voice. "bench-set" runs a folder of matching clean and noisy clips
/// (for example a public speech enhancement test set) and averages the results. Everything stays on this computer, and
/// only the file you ask for is written.
/// </summary>
internal static class LabCommand
{
    private const int Rate = 48000;
    private const int MaxDelaySamples = 4800;
    private const int SetMaxDelaySamples = 1500;

    private static readonly (string Name, Func<IFrameDenoiser> Factory, float Strength)[] Engines =
    {
        ("RNNoise, strength 100%", () => new RnNoiseDenoiser(), 1f),
        ("RNNoise, strength 70%", () => new RnNoiseDenoiser(), 0.7f),
    };

    public static int Run(DeviceEnumerator devices, string[] args)
    {
        if (args.Length >= 1 && args[0].Equals("record", StringComparison.OrdinalIgnoreCase))
        {
            return Record(devices, args.Skip(1).ToArray());
        }

        if (args.Length >= 3 && args[0].Equals("bench", StringComparison.OrdinalIgnoreCase))
        {
            return Bench(args[1], args[2]);
        }

        if (args.Length >= 3 && args[0].Equals("bench-set", StringComparison.OrdinalIgnoreCase))
        {
            var max = args.Length > 3 && int.TryParse(args[3], out var parsed) && parsed > 0 ? parsed : 60;
            return BenchSet(args[1], args[2], max);
        }

        Console.WriteLine("Usage:");
        Console.WriteLine("  Audora.Cli lab record <seconds 1-60> <file.wav> [part of microphone name]");
        Console.WriteLine("  Audora.Cli lab bench <voice.wav> <noise.wav>");
        Console.WriteLine("  Audora.Cli lab bench-set <clean folder> <noisy folder> [number of clips, default 60]");
        return 1;
    }

    private static int Record(DeviceEnumerator devices, string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out var seconds) || seconds < 1 || seconds > 60)
        {
            Console.WriteLine("Usage: Audora.Cli lab record <seconds 1-60> <file.wav> [part of microphone name]");
            return 1;
        }

        var file = args[1];
        var term = string.Join(' ', args.Skip(2)).Trim();
        var active = devices.GetCaptureDevices().Where(d => d.Status == DeviceStatus.Active).ToList();
        var matches = term.Length == 0
            ? active.Where(d => d.IsDefault).ToList()
            : active.Where(d => d.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count != 1)
        {
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
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDevice(matches[0].Id);
            using var capture = new WasapiCapture(device);
            using var writer = new WaveFileWriter(file, capture.WaveFormat);
            var gate = new object();
            capture.DataAvailable += (_, e) =>
            {
                lock (gate)
                {
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                }
            };

            Console.WriteLine($"Recording {seconds} seconds from {matches[0].Name} to {file}.");
            Console.WriteLine("The file holds what the microphone hears. Audora does not send it anywhere. Delete it when you are done.");
            capture.StartRecording();
            Thread.Sleep(seconds * 1000);
            capture.StopRecording();
            Thread.Sleep(300);
            lock (gate)
            {
                writer.Flush();
            }

            Console.WriteLine("Done.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(MicrophoneSession.DescribeStartError(ex));
            return 1;
        }
    }

    private static int Bench(string voicePath, string noisePath)
    {
        float[] voice;
        float[] noise;
        try
        {
            voice = LoadMono48k(voicePath);
            noise = LoadMono48k(noisePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not read the files: {ex.Message}");
            return 1;
        }

        if (voice.Length < Rate * 3 || noise.Length < Rate)
        {
            Console.WriteLine("Use a voice file of at least 3 seconds and a noise file of at least 1 second.");
            return 1;
        }

        Console.WriteLine($"Voice {voice.Length / (double)Rate:0.0} s, noise {noise.Length / (double)Rate:0.0} s. Measured on this computer; nothing is sent.");

        foreach (var engine in Engines)
        {
            Console.WriteLine();
            Console.WriteLine(engine.Name);
            try
            {
                MeasureEngine(voice, noise, engine.Factory, engine.Strength);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Could not run: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("How to read it: a higher clarity gain is better. For the voice alone, a level change near 0 dB and a similarity");
        Console.WriteLine("above about 20 dB mean the voice is kept, if your voice clip was recorded in a quiet room. A smaller delay is better.");
        return 0;
    }

    private static int BenchSet(string cleanDir, string noisyDir, int maxClips)
    {
        if (!Directory.Exists(cleanDir) || !Directory.Exists(noisyDir))
        {
            Console.WriteLine("Both folders must exist. Usage: Audora.Cli lab bench-set <clean folder> <noisy folder> [number of clips]");
            return 1;
        }

        var all = Directory.GetFiles(noisyDir, "*.wav").OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        var pairs = all
            .Select(noisy => (Noisy: noisy, Clean: Path.Combine(cleanDir, Path.GetFileName(noisy))))
            .Where(p => File.Exists(p.Clean))
            .ToList();
        if (pairs.Count == 0)
        {
            Console.WriteLine("No clip with the same file name was found in both folders.");
            return 1;
        }

        var step = Math.Max(1, pairs.Count / maxClips);
        var chosen = pairs.Where((_, i) => i % step == 0).Take(maxClips).ToList();
        Console.WriteLine($"{pairs.Count} matching clips found, using {chosen.Count} spread across the set. Measured on this computer; nothing is sent.");

        var clips = new List<(float[] Clean, float[] Noisy)>();
        foreach (var pair in chosen)
        {
            try
            {
                clips.Add((LoadMono48k(pair.Clean), LoadMono48k(pair.Noisy)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Skipped {Path.GetFileName(pair.Noisy)}: {ex.Message}");
            }
        }

        if (clips.Count == 0)
        {
            Console.WriteLine("No clip could be read.");
            return 1;
        }

        var audioSeconds = clips.Sum(c => c.Noisy.Length) / (double)Rate;
        Console.WriteLine($"{clips.Count} clips, {audioSeconds:0} seconds of noisy speech in total.");

        foreach (var engine in Engines)
        {
            Console.WriteLine();
            Console.WriteLine(engine.Name);
            try
            {
                MeasureSet(clips, engine.Factory, engine.Strength, audioSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Could not run: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("How to read it: clarity is how close the sound is to the clean recording (higher is better, gain is the change).");
        Console.WriteLine("Clean speech kept: the same engine run on clean recordings. A level change near 0 dB and a high similarity mean");
        Console.WriteLine("it does not damage speech. Speed is how many times faster than real time it runs on one core.");
        return 0;
    }

    private static void MeasureSet(List<(float[] Clean, float[] Noisy)> clips, Func<IFrameDenoiser> factory, float strength, double audioSeconds)
    {
        var delay = SignalMetrics.BestDelay(clips[0].Clean, RunEngine(clips[0].Clean, factory, strength), SetMaxDelaySamples);
        double before = 0;
        double after = 0;
        double kept = 0;
        double levelChange = 0;
        var processing = new Stopwatch();

        foreach (var (clean, noisy) in clips)
        {
            processing.Start();
            var noisyOut = RunEngine(noisy, factory, strength);
            processing.Stop();
            var length = Math.Min(clean.Length, Math.Min(noisy.Length, noisyOut.Length - delay));
            before += SignalMetrics.SiSdr(clean.AsSpan(0, length), noisy.AsSpan(0, length));
            after += SignalMetrics.SiSdr(clean.AsSpan(0, length), noisyOut.AsSpan(delay, length));

            var cleanOut = RunEngine(clean, factory, strength);
            var cleanLength = Math.Min(clean.Length, cleanOut.Length - delay);
            kept += SignalMetrics.SiSdr(clean.AsSpan(0, cleanLength), cleanOut.AsSpan(delay, cleanLength));
            levelChange += SignalMetrics.Db(SignalMetrics.Rms(cleanOut.AsSpan(delay, cleanLength)) / Math.Max(SignalMetrics.Rms(clean.AsSpan(0, cleanLength)), 1e-9));
        }

        var n = clips.Count;
        var speed = audioSeconds / Math.Max(processing.Elapsed.TotalSeconds, 1e-6);
        Console.WriteLine($"  Noisy speech: clarity {before / n:0.0} to {after / n:0.0} dB (gain {(after - before) / n:+0.0;-0.0} dB)");
        Console.WriteLine($"  Clean speech kept: similarity {kept / n:0.0} dB, level change {levelChange / n:0.0} dB");
        Console.WriteLine($"  Delay {delay / 48.0:0.0} ms, speed {speed:0} times real time");
    }

    private static void MeasureEngine(float[] voice, float[] noise, Func<IFrameDenoiser> factory, float strength)
    {
        var voiceOut = RunEngine(voice, factory, strength);
        var delay = SignalMetrics.BestDelay(voice, voiceOut, MaxDelaySamples);
        var voiceLength = Math.Min(voice.Length, voiceOut.Length - delay);
        var voiceReference = voice.AsSpan(0, voiceLength);
        var voiceEstimate = voiceOut.AsSpan(delay, voiceLength);
        var levelChange = SignalMetrics.Db(SignalMetrics.Rms(voiceEstimate) / Math.Max(SignalMetrics.Rms(voiceReference), 1e-9));
        Console.WriteLine($"  Voice alone: level change {levelChange:0.0} dB, similarity {SignalMetrics.SiSdr(voiceReference, voiceEstimate):0.0} dB, delay {delay / 48.0:0.0} ms");

        foreach (var snr in new[] { 0.0, 5.0, 10.0 })
        {
            var mixture = SignalMetrics.MixAtSnr(voice, noise, snr, out var scaledNoise);
            var mixtureOut = RunEngine(mixture, factory, strength);
            var length = Math.Min(voice.Length, mixtureOut.Length - delay);
            var before = SignalMetrics.SiSdr(voice.AsSpan(0, length), mixture.AsSpan(0, length));
            var after = SignalMetrics.SiSdr(voice.AsSpan(0, length), mixtureOut.AsSpan(delay, length));
            Console.WriteLine($"  Noise {snr:0} dB below the voice: clarity {before:0.0} to {after:0.0} dB ({after - before:+0.0;-0.0} dB)");

            if (snr == 5.0)
            {
                var noiseOut = RunEngine(scaledNoise, factory, strength);
                var noiseLength = Math.Min(scaledNoise.Length, noiseOut.Length - delay);
                var change = SignalMetrics.Db(SignalMetrics.Rms(noiseOut.AsSpan(delay, noiseLength)) / Math.Max(SignalMetrics.Rms(scaledNoise.AsSpan(0, noiseLength)), 1e-9));
                Console.WriteLine($"  Noise alone: change {change:0.0} dB (below 0 is quieter)");
            }
        }
    }

    private static float[] RunEngine(float[] input, Func<IFrameDenoiser> factory, float strength)
    {
        using var cleaner = new MicrophoneCleaner(Rate, 1, factory);
        cleaner.Strength = strength;
        cleaner.Enabled = true;
        if (cleaner.Failed)
        {
            throw new InvalidOperationException(cleaner.UnavailableReason ?? "Noise removal could not start.");
        }

        var output = new List<float>(input.Length);
        const int chunk = 960;
        for (var start = 0; start < input.Length; start += chunk)
        {
            var count = Math.Min(chunk, input.Length - start);
            var bytes = new byte[count * sizeof(float)];
            Buffer.BlockCopy(input, start * sizeof(float), bytes, 0, bytes.Length);
            cleaner.Push(bytes, bytes.Length);
            cleaner.ReadAvailable((buffer, produced) =>
            {
                for (var i = 0; i < produced; i++)
                {
                    output.Add(buffer[i]);
                }
            });
        }

        return output.ToArray();
    }

    private static float[] LoadMono48k(string path)
    {
        using var reader = new AudioFileReader(path);
        ISampleProvider source = reader;
        if (reader.WaveFormat.Channels == 2)
        {
            source = new StereoToMonoSampleProvider(source);
        }
        else if (reader.WaveFormat.Channels != 1)
        {
            throw new InvalidOperationException("Use a mono or stereo file.");
        }

        if (source.WaveFormat.SampleRate != Rate)
        {
            source = new WdlResamplingSampleProvider(source, Rate);
        }

        var samples = new List<float>();
        var buffer = new float[Rate];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                samples.Add(buffer[i]);
            }
        }

        return samples.ToArray();
    }
}
