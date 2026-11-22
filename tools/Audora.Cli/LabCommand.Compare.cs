using Audora.Core.Cleanup;

namespace Audora.Cli;

// Comparing sound that another program has already cleaned (for example the DeepFilterNet command-line tool).
internal static partial class LabCommand
{
    private static List<(string Noisy, string Clean)> PickPairs(string cleanDir, string noisyDir, int maxClips)
    {
        var all = Directory.GetFiles(noisyDir, "*.wav").OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        var pairs = all
            .Select(noisy => (Noisy: noisy, Clean: Path.Combine(cleanDir, Path.GetFileName(noisy))))
            .Where(p => File.Exists(p.Clean))
            .ToList();
        var step = Math.Max(1, pairs.Count / Math.Max(1, maxClips));
        return pairs.Where((_, i) => i % step == 0).Take(maxClips).ToList();
    }

    private static int Subset(string cleanDir, string noisyDir, string outDir, int count)
    {
        if (!Directory.Exists(cleanDir) || !Directory.Exists(noisyDir))
        {
            Console.WriteLine("Both input folders must exist.");
            return 1;
        }

        var picked = PickPairs(cleanDir, noisyDir, count);
        if (picked.Count == 0)
        {
            Console.WriteLine("No clip with the same file name was found in both folders.");
            return 1;
        }

        var cleanOut = Path.Combine(outDir, "clean");
        var noisyOut = Path.Combine(outDir, "noisy");
        Directory.CreateDirectory(cleanOut);
        Directory.CreateDirectory(noisyOut);
        foreach (var pair in picked)
        {
            var name = Path.GetFileName(pair.Noisy);
            File.Copy(pair.Clean, Path.Combine(cleanOut, name), true);
            File.Copy(pair.Noisy, Path.Combine(noisyOut, name), true);
        }

        Console.WriteLine($"Copied {picked.Count} matching clips, spread across the set, to:");
        Console.WriteLine($"  {cleanOut}");
        Console.WriteLine($"  {noisyOut}");
        return 0;
    }

    private static int CompareSet(string cleanDir, string noisyDir, string enhancedNoisyDir, string? enhancedCleanDir)
    {
        if (!Directory.Exists(cleanDir) || !Directory.Exists(noisyDir) || !Directory.Exists(enhancedNoisyDir)
            || (enhancedCleanDir is not null && !Directory.Exists(enhancedCleanDir)))
        {
            Console.WriteLine("All the folders must exist.");
            return 1;
        }

        var rows = new List<(float[] Clean, float[] Noisy, float[] Enhanced, float[]? EnhancedClean)>();
        var missing = 0;
        foreach (var pair in PickPairs(cleanDir, noisyDir, int.MaxValue))
        {
            var name = Path.GetFileName(pair.Noisy);
            var enhancedPath = Path.Combine(enhancedNoisyDir, name);
            if (!File.Exists(enhancedPath))
            {
                missing++;
                continue;
            }

            try
            {
                float[]? enhancedClean = null;
                if (enhancedCleanDir is not null)
                {
                    var enhancedCleanPath = Path.Combine(enhancedCleanDir, name);
                    if (File.Exists(enhancedCleanPath))
                    {
                        enhancedClean = LoadMono48k(enhancedCleanPath);
                    }
                }

                rows.Add((LoadMono48k(pair.Clean), LoadMono48k(pair.Noisy), LoadMono48k(enhancedPath), enhancedClean));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Skipped {name}: {ex.Message}");
            }
        }

        if (rows.Count == 0)
        {
            Console.WriteLine("No cleaned file with a matching name was found. Check that the enhanced folder holds files named like the noisy ones.");
            return 1;
        }

        if (missing > 0)
        {
            Console.WriteLine($"{missing} noisy clips had no cleaned file and were left out.");
        }

        var delay = SignalMetrics.BestDelay(rows[0].Noisy, rows[0].Enhanced, SetMaxDelaySamples);
        double before = 0;
        double after = 0;
        double kept = 0;
        double levelChange = 0;
        var keptCount = 0;
        foreach (var (clean, noisy, enhanced, enhancedClean) in rows)
        {
            var length = Math.Min(clean.Length, Math.Min(noisy.Length, enhanced.Length - delay));
            before += SignalMetrics.SiSdr(clean.AsSpan(0, length), noisy.AsSpan(0, length));
            after += SignalMetrics.SiSdr(clean.AsSpan(0, length), enhanced.AsSpan(delay, length));

            if (enhancedClean is not null)
            {
                var cleanLength = Math.Min(clean.Length, enhancedClean.Length - delay);
                kept += SignalMetrics.SiSdr(clean.AsSpan(0, cleanLength), enhancedClean.AsSpan(delay, cleanLength));
                levelChange += SignalMetrics.Db(SignalMetrics.Rms(enhancedClean.AsSpan(delay, cleanLength)) / Math.Max(SignalMetrics.Rms(clean.AsSpan(0, cleanLength)), 1e-9));
                keptCount++;
            }
        }

        var n = rows.Count;
        Console.WriteLine($"{n} clips compared. Measured on this computer; nothing is sent.");
        Console.WriteLine($"  Noisy speech: clarity {before / n:0.0} to {after / n:0.0} dB (gain {(after - before) / n:+0.0;-0.0} dB)");
        if (keptCount > 0)
        {
            Console.WriteLine($"  Clean speech kept: similarity {kept / keptCount:0.0} dB, level change {levelChange / keptCount:0.0} dB");
        }
        else
        {
            Console.WriteLine("  Clean speech kept: not measured (no cleaned versions of the clean clips were given).");
        }

        Console.WriteLine($"  Delay found: {delay / 48.0:0.0} ms");
        return 0;
    }
}
