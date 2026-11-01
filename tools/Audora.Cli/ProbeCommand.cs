using System.Diagnostics;
using Audora.Core.Audio;
using Audora.Core.Devices;

namespace Audora.Cli;

/// <summary>
/// 15-second diagnostic: starts loopback capture alone, then adds one output
/// at second 3 and another at second 6, reporting captured data and level every second.
/// </summary>
internal static class ProbeCommand
{
    public static int Run(DeviceEnumerator devices, string[] terms)
    {
        var candidates = devices.GetRenderDevices()
            .Where(d => d.Status == DeviceStatus.Active && !d.IsDefault)
            .ToList();

        var targets = new List<AudioDeviceInfo>();
        foreach (var term in terms)
        {
            var matches = candidates
                .Where(d => d.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count != 1)
            {
                Console.WriteLine($"\"{term}\" must match exactly one active, non-default device (matched {matches.Count}).");
                return 1;
            }

            targets.Add(matches[0]);
        }

        var clock = Stopwatch.StartNew();
        string Stamp() => $"[{clock.Elapsed.TotalSeconds,5:0.0}s]";

        using var engine = new MirrorEngine();
        engine.CaptureStopped += (_, ex) =>
        {
            var code = ex is null ? "none" : $"0x{ex.HResult:X8}";
            Console.WriteLine($"{Stamp()} CAPTURE STOPPED  error={ex?.GetType().Name ?? "none"}  hresult={code}");
        };

        engine.Start();
        Console.WriteLine($"{Stamp()} capture started on: {engine.SourceName}");
        Console.WriteLine($"{Stamp()} format: {engine.CaptureFormat}");
        Console.WriteLine("Keep music or a video playing for the whole test.");

        var next = 0;
        for (var second = 1; second <= 15; second++)
        {
            Thread.Sleep(1000);

            if (second % 3 == 0 && next < targets.Count)
            {
                var target = targets[next++];
                try
                {
                    engine.AddOutput(target.Id, target.Name);
                    Console.WriteLine($"{Stamp()} added output: {target.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{Stamp()} FAILED to add {target.Name}: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}");
                }
            }

            Console.WriteLine($"{Stamp()} captured {engine.CapturedBytes / 1024,8} KB   peak {engine.TakePeakLevel(),4:0.00}   outputs {engine.Outputs.Count}");
        }

        return 0;
    }
}
