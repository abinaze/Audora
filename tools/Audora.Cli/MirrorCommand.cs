using Audora.Core.Audio;
using Audora.Core.Devices;

namespace Audora.Cli;

internal static class MirrorCommand
{
    private const string FromPrefix = "from:";

    public static int Run(DeviceEnumerator devices, string[] args)
    {
        var sourceTerm = args
            .Where(a => a.StartsWith(FromPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(a => a[FromPrefix.Length..].Trim())
            .FirstOrDefault();
        var terms = args
            .Where(a => !a.StartsWith(FromPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (terms.Length == 0)
        {
            Console.WriteLine("Usage: Audora.Cli mirror [from:<source name>] <target name> [more targets...]");
            Console.WriteLine("  Source = the device your apps play to (default: the Windows default output).");
            Console.WriteLine("  Targets = the other devices that should play the same audio.");
            Console.WriteLine("Example: Audora.Cli mirror \"from:Headphones (MT)\" Chu2 \"Headphones (Realtek\"");
            return 1;
        }

        var active = devices.GetRenderDevices().Where(d => d.Status == DeviceStatus.Active).ToList();

        AudioDeviceInfo? source;
        if (string.IsNullOrWhiteSpace(sourceTerm))
        {
            source = active.FirstOrDefault(d => d.IsDefault);
            if (source is null)
            {
                Console.WriteLine("No default output device found.");
                return 1;
            }
        }
        else
        {
            var sourceMatches = active
                .Where(d => d.Name.Contains(sourceTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sourceMatches.Count != 1)
            {
                PrintMatchProblem("source", sourceTerm, sourceMatches, active);
                return 1;
            }

            source = sourceMatches[0];
        }

        var candidates = active
            .Where(d => !string.Equals(d.Id, source.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var targets = new List<AudioDeviceInfo>();

        foreach (var term in terms)
        {
            var matches = candidates
                .Where(d => d.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count != 1)
            {
                PrintMatchProblem("target", term, matches, candidates);
                return 1;
            }

            if (!targets.Contains(matches[0]))
            {
                targets.Add(matches[0]);
            }
        }

        using var engine = new MirrorEngine();
        engine.Start(source.Id);
        Console.WriteLine($"Capturing from: {engine.SourceName}{(source.IsDefault ? " (Windows default)" : "")}");
        if (!source.IsDefault)
        {
            Console.WriteLine("Only audio playing to that device is captured. Route your app to it in Windows:");
            Console.WriteLine("  Settings > System > Sound > Volume mixer > your app > Output device.");
        }
        Console.WriteLine($"Capture format: {engine.CaptureFormat}");
        engine.CaptureStopped += (_, ex) =>
            Console.WriteLine($"[!] Capture stopped: {ex?.Message ?? "device changed or went away"}");

        var channels = new List<OutputChannel>();
        foreach (var target in targets)
        {
            var channel = engine.AddOutput(target.Id, target.Name);
            channel.Faulted += (_, ex) => Console.WriteLine($"[!] {channel.Name} stopped: {ex.Message}");
            channels.Add(channel);
        }

        Console.WriteLine();
        Console.WriteLine("Play some audio (music or a video). Commands:");
        Console.WriteLine("  l                  list outputs");
        Console.WriteLine("  v <n> <0-100>      volume of output n");
        Console.WriteLine("  m <n>              mute/unmute output n");
        Console.WriteLine("  b <n> <-100..100>  balance of output n (-100 left, 100 right)");
        Console.WriteLine("  d <n> <0-2000>     delay output n in milliseconds");
        Console.WriteLine("  q                  quit");
        Console.WriteLine();
        PrintChannels(channels);

        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line is null)
            {
                break;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            var cmd = parts[0].ToLowerInvariant();
            if (cmd is "q" or "quit" or "exit")
            {
                break;
            }

            if (cmd is "l" or "list")
            {
                PrintChannels(channels);
                continue;
            }

            if (cmd is "v" or "m" or "b" or "d"
                && parts.Length >= 2
                && int.TryParse(parts[1], out var n)
                && n >= 1 && n <= channels.Count)
            {
                var selected = channels[n - 1];

                if (cmd == "m")
                {
                    selected.IsMuted = !selected.IsMuted;
                }
                else if (parts.Length >= 3 && int.TryParse(parts[2], out var value))
                {
                    if (cmd == "d")
                    {
                        selected.DelayMilliseconds = value;
                    }
                    else if (cmd == "v")
                    {
                        selected.Volume = Math.Clamp(value, 0, 100) / 100f;
                    }
                    else
                    {
                        selected.Balance = Math.Clamp(value, -100, 100) / 100f;
                    }
                }
                else
                {
                    Console.WriteLine("Missing value. Example: v 1 40");
                    continue;
                }

                PrintChannels(channels);
                continue;
            }

            Console.WriteLine("Unknown command. Type l to list, q to quit.");
        }

        return 0;
    }

    private static void PrintMatchProblem(string role, string term, List<AudioDeviceInfo> matches, List<AudioDeviceInfo> pool)
    {
        if (matches.Count == 0)
        {
            Console.WriteLine($"No active device matches {role} \"{term}\". Devices you can use here:");
            foreach (var d in pool)
            {
                Console.WriteLine($"  {d.Name}");
            }

            if (role == "target")
            {
                Console.WriteLine("  (The source device is excluded: it cannot also be a target.)");
            }
        }
        else
        {
            Console.WriteLine($"\"{term}\" matches several devices; be more specific:");
            foreach (var d in matches)
            {
                Console.WriteLine($"  {d.Name}");
            }
        }
    }

    private static void PrintChannels(List<OutputChannel> channels)
    {
        for (var i = 0; i < channels.Count; i++)
        {
            var c = channels[i];
            var muted = c.IsMuted ? "MUTED" : "     ";
            Console.WriteLine($"  {i + 1}. {c.Name,-44} vol {c.Volume * 100,3:0}%  {muted}  balance {c.Balance * 100:+0;-0;0}  delay {c.DelayMilliseconds} ms");
        }
    }
}
