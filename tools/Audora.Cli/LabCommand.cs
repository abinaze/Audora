using Audora.Core.Audio;
using Audora.Core.Cleanup;
using Audora.Core.Devices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Audora.Cli;

/// <summary>
/// Test tools for comparing noise removal engines with numbers. "record" saves a short clip from a microphone to a file
/// you name. "bench" mixes your own noise into your own voice and prints how each engine does. Everything stays on this
/// computer, and only the file you ask for is written.
/// </summary>
internal static class LabCommand
{
    private const int Rate = 48000;
    private const int MaxDelaySamples = 4800;

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

        Console.WriteLine("Usage:");
        Console.WriteLine("  Audora.Cli lab record <seconds 1-60> <file.wav> [part of microphone name]");
        Console.WriteLine("  Audora.Cli lab bench <voice.wav> <noise.wav>");
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

        var engines = new (string Name, Func<IFrameDenoiser> Factory, float Strength)[]
        {
            ("RNNoise, strength 100%", () => new RnNoiseDenoiser(), 1f),
            ("RNNoise, strength 70%", () => new RnNoiseDenoiser(), 0.7f),
        };

        foreach (var engine in engines)
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
        Console.WriteLine("above about 20 dB mean the voice is kept. A smaller delay is better.");
        return 0;
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
