using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Cli;

/// <summary>
/// Plays a short, quiet test tone so you can hear one side or a sweep. It never changes the Windows volume.
/// It refuses to play when the Windows volume is high, unless you add --ok.
/// </summary>
internal static class ToneCommand
{
    private const float LoudVolume = 0.5f;

    private static readonly Dictionary<string, ToneRequest> Kinds = new()
    {
        ["left"] = new ToneRequest(ToneChannel.Left, ToneShape.Steady, 440, 3),
        ["right"] = new ToneRequest(ToneChannel.Right, ToneShape.Steady, 440, 3),
        ["both"] = new ToneRequest(ToneChannel.Both, ToneShape.Steady, 440, 3),
        ["sweep"] = new ToneRequest(ToneChannel.Both, ToneShape.Sweep, 0, 6)
    };

    private static readonly Dictionary<string, string> Hints = new()
    {
        ["left"] = "You should hear a steady tone from the LEFT side only.",
        ["right"] = "You should hear a steady tone from the RIGHT side only.",
        ["both"] = "You should hear the same steady tone from BOTH sides.",
        ["sweep"] = "You should hear a smooth tone that rises from low to high."
    };

    public static int Run(DeviceEnumerator devices, string[] args, bool allowLoud)
    {
        var kind = args.FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;
        var active = devices.GetActiveRenderDevices().ToList();

        if (!Kinds.TryGetValue(kind, out var request))
        {
            Console.WriteLine("Usage: Audora.Cli tone <left|right|both|sweep> [part of device name] [--ok]");
            Console.WriteLine("Plays a short, quiet tone. Without a device name it uses the Windows default output.");
            Console.WriteLine("If the Windows volume is above 50% it refuses, unless you add --ok.");
            return 1;
        }

        var term = string.Join(' ', args.Skip(1)).Trim();
        var matches = term.Length == 0
            ? active.Where(d => d.IsDefault).ToList()
            : active.Where(d => d.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count != 1)
        {
            Console.WriteLine(matches.Count == 0
                ? "No matching active device. Active devices:"
                : $"\"{term}\" matches several devices; be more specific:");
            foreach (var d in matches.Count == 0 ? active : matches)
            {
                Console.WriteLine($"  {d.Name}");
            }

            return 1;
        }

        var target = matches[0];
        using var player = new SoundCheckPlayer();

        var state = player.ReadWindowsVolume(target.Id);
        if (state is not null && state.Muted)
        {
            Console.WriteLine($"{target.Name} is muted in Windows, so you would hear nothing. Unmute it and try again.");
            return 1;
        }

        if (state is not null && state.Volume > LoudVolume && !allowLoud)
        {
            Console.WriteLine($"The Windows volume of {target.Name} is {state.Volume * 100:0}%. A test tone could be loud.");
            Console.WriteLine("Lower it to 50% or less in Windows, or add --ok to play anyway. Audora never changes your volume.");
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

        Console.WriteLine($"Playing a quiet {kind} test on {target.Name} ({request.Seconds:0} seconds). Press Ctrl+C to stop at once.");
        Console.WriteLine(Hints[kind]);

        player.Play(target.Id, request);
        done.Wait(TimeSpan.FromSeconds(request.Seconds + 5));
        player.Stop();

        Console.WriteLine("Done.");
        return 0;
    }
}
