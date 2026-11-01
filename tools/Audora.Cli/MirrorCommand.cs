using Audora.Core.Audio;
using Audora.Core.Devices;

namespace Audora.Cli;

internal static class MirrorCommand
{
    public static int Run(DeviceEnumerator devices, string[] terms)
    {
        if (terms.Length == 0)
        {
            Console.WriteLine("Usage: Audora.Cli mirror <part of device name> [more names...]");
            Console.WriteLine("Example: Audora.Cli mirror \"Headphones (MT)\" Chu2");
            return 1;
        }

        var all = devices.GetRenderDevices();
        if (!all.Any(d => d.IsDefault))
        {
            Console.WriteLine("No default output device found.");
            return 1;
        }

        var candidates = all.Where(d => d.Status == DeviceStatus.Active && !d.IsDefault).ToList();
        var targets = new List<AudioDeviceInfo>();

        foreach (var term in terms)
        {
            var matches = candidates
                .Where(d => d.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                Console.WriteLine($"No active, non-default device matches \"{term}\".");
                return 1;
            }

            if (matches.Count > 1)
            {
                Console.WriteLine($"\"{term}\" matches several devices; be more specific:");
                foreach (var m in matches)
                {
                    Console.WriteLine($"  {m.Name}");
                }
                return 1;
            }

            if (!targets.Contains(matches[0]))
            {
                targets.Add(matches[0]);
            }
        }

        using var engine = new MirrorEngine();
        engine.Start();
        Console.WriteLine($"Capturing the default output: {engine.SourceName}");
        Console.WriteLine($"Capture format: {engine.CaptureFormat}");
        engine.CaptureStopped += (_, ex) =>
            Console.WriteLine($"[!] Capture stopped: {ex?.Message ?? "default device changed or went away"}");

        var channels = new List<OutputChannel>();
        foreach (var target in targets)
        {
            var channel = engine.AddOutput(target.Id, target.Name);
            channel.Faulted += (_, ex) => Console.WriteLine($"[!] {channel.Name} stopped: {ex.Message}");
            channels.Add(channel);
        }

        Console.WriteLine();
        Console.WriteLine("Play some audio (music or a video). Commands:");
        Console.WriteLine("  l               list outputs");
        Console.WriteLine("  v <n> <0-100>   volume of output n");
        Console.WriteLine("  m <n>           mute/unmute output n");
        Console.WriteLine("  b <n> <-100..100>  balance of output n (-100 left, 100 right)");
        Console.WriteLine("  q               quit");
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

            if (cmd is "v" or "m" or "b"
                && parts.Length >= 2
                && int.TryParse(parts[1], out var n)
                && n >= 1 && n <= channels.Count)
            {
                var channel = channels[n - 1];

                if (cmd == "m")
                {
                    channel.IsMuted = !channel.IsMuted;
                }
                else if (parts.Length >= 3 && int.TryParse(parts[2], out var value))
                {
                    if (cmd == "v")
                    {
                        channel.Volume = Math.Clamp(value, 0, 100) / 100f;
                    }
                    else
                    {
                        channel.Balance = Math.Clamp(value, -100, 100) / 100f;
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

    private static void PrintChannels(List<OutputChannel> channels)
    {
        for (var i = 0; i < channels.Count; i++)
        {
            var c = channels[i];
            var muted = c.IsMuted ? "MUTED" : "     ";
            Console.WriteLine($"  {i + 1}. {c.Name,-44} vol {c.Volume * 100,3:0}%  {muted}  balance {c.Balance * 100:+0;-0;0}");
        }
    }
}
