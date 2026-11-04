using Audora.Core.Audio;
using Audora.Core.Devices;

namespace Audora.Cli;

internal static class RunCommand
{
    public static int Run(DeviceEnumerator devices, string[] terms)
    {
        if (terms.Length == 0)
        {
            Console.WriteLine("Usage: Audora.Cli run <device name> [more devices...]");
            Console.WriteLine("  Plays your system audio on every device you name, and keeps it going");
            Console.WriteLine("  when devices disconnect, reconnect or the Windows default changes.");
            Console.WriteLine("Example: Audora.Cli run \"Headphones (MT)\" Chu2 \"Headphones (Realtek\"");
            return 1;
        }

        var active = devices.GetActiveRenderDevices().ToList();
        var targets = new List<AudioDeviceInfo>();

        foreach (var term in terms)
        {
            var matches = active
                .Where(d => d.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count != 1)
            {
                Console.WriteLine(matches.Count == 0
                    ? $"No active device matches \"{term}\". Active devices:"
                    : $"\"{term}\" matches several devices; be more specific:");
                foreach (var d in matches.Count == 0 ? active : matches)
                {
                    Console.WriteLine($"  {d.Name}");
                }
                return 1;
            }

            if (!targets.Contains(matches[0]))
            {
                targets.Add(matches[0]);
            }
        }

        using var controller = new MirrorController(devices);
        controller.Message += (_, text) => Console.WriteLine($"[{DateTime.Now:T}] {text}");

        foreach (var target in targets)
        {
            controller.Select(target.Id);
        }

        controller.Start();

        Console.WriteLine();
        Console.WriteLine("Running. Play music, then try disconnecting and reconnecting devices.");
        Console.WriteLine("  s                  status of every device");
        Console.WriteLine("  v <n> <0-100>      volume of device n");
        Console.WriteLine("  m <n>              mute/unmute device n");
        Console.WriteLine("  b <n> <-100..100>  balance of device n");
        Console.WriteLine("  d <n> <0-2000>     delay of device n in milliseconds");
        Console.WriteLine("  q                  quit");
        Console.WriteLine();
        Print(controller, targets);

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

            if (cmd is "s" or "l" or "list")
            {
                Print(controller, targets);
                continue;
            }

            if (cmd is "v" or "m" or "b" or "d"
                && parts.Length >= 2
                && int.TryParse(parts[1], out var n)
                && n >= 1 && n <= targets.Count)
            {
                var id = targets[n - 1].Id;

                if (cmd == "m")
                {
                    controller.SetMuted(id, !controller.GetSettings(id).Muted);
                }
                else if (parts.Length >= 3 && int.TryParse(parts[2], out var value))
                {
                    if (cmd == "v")
                    {
                        controller.SetVolume(id, value / 100f);
                    }
                    else if (cmd == "b")
                    {
                        controller.SetBalance(id, value / 100f);
                    }
                    else
                    {
                        controller.SetDelay(id, value);
                    }
                }
                else
                {
                    Console.WriteLine("Missing value. Example: v 1 40");
                    continue;
                }

                Print(controller, targets);
                continue;
            }

            Console.WriteLine("Unknown command. Type s for status, q to quit.");
        }

        return 0;
    }

    private static void Print(MirrorController controller, List<AudioDeviceInfo> targets)
    {
        var status = controller.GetStatus()
            .ToDictionary(s => s.DeviceId, s => s.State, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var settings = controller.GetSettings(target.Id);
            var state = status.TryGetValue(target.Id, out var s) ? s : "?";
            var muted = settings.Muted ? " MUTED" : "";

            Console.WriteLine(
                $"  {i + 1}. {target.Name,-38} {state,-34} vol {settings.Volume * 100,3:0}%{muted}  bal {settings.Balance * 100:+0;-0;0}  delay {settings.DelayMilliseconds} ms");
        }
    }
}
