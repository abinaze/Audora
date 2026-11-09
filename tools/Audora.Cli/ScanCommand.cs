using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Cli;

/// <summary>Runs the read-only quick scan and prints what it found. Nothing is changed.</summary>
internal static class ScanCommand
{
    public static int Run(DeviceEnumerator devices, bool showAll)
    {
        Console.WriteLine("Audora scan (read only: nothing is changed)");
        Console.WriteLine();

        var report = new AudioScanner(devices).Run();

        Console.WriteLine("Findings");
        foreach (var f in report.Findings)
        {
            Console.WriteLine($"  [{Label(f.Level)}] {f.Area}: {f.Title}");
            if (f.Level != ScanLevel.Ok && f.Detail.Length > 0)
            {
                Console.WriteLine($"             {f.Detail}");
            }
        }

        Console.WriteLine();
        PrintDevices("Outputs (* = Windows default)", report.Input.RenderDevices, showAll);
        PrintDevices("Inputs (* = Windows default)", report.Input.CaptureDevices, showAll);

        Console.WriteLine($"  {report.Count(ScanLevel.Problem)} problem(s), {report.Count(ScanLevel.Warning)} warning(s), {report.Count(ScanLevel.Info)} note(s)");
        return report.Worst == ScanLevel.Problem ? 2 : 0;
    }

    private static string Label(ScanLevel level) => level switch
    {
        ScanLevel.Ok => "OK     ",
        ScanLevel.Info => "NOTE   ",
        ScanLevel.Warning => "WARNING",
        _ => "PROBLEM"
    };

    private static void PrintDevices(string title, IReadOnlyList<AudioDeviceInfo> devices, bool showAll)
    {
        var shown = showAll ? devices : devices.Where(d => d.Status != DeviceStatus.NotPresent).ToList();

        Console.WriteLine(title);
        if (shown.Count == 0)
        {
            Console.WriteLine("  (none)");
        }

        foreach (var d in shown)
        {
            var marker = d.IsDefault ? "*" : " ";
            Console.WriteLine($"{marker} {d.Name,-52} {d.Status,-11}");
        }

        var hidden = devices.Count - shown.Count;
        if (hidden > 0)
        {
            Console.WriteLine($"  ({hidden} old records hidden; add --all to show them)");
        }

        Console.WriteLine();
    }
}
