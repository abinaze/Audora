using Audora.Core.Devices;

namespace Audora.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        var showAll = args.Contains("--all", StringComparer.OrdinalIgnoreCase);
        var command = args.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant() ?? "list";
        using var devices = new DeviceEnumerator();

        switch (command)
        {
            case "list":
                PrintList(devices, showAll);
                return 0;

            case "watch":
                return Watch(devices, showAll);

            case "mirror":
                return MirrorCommand.Run(devices, args.Skip(1).Where(a => !a.StartsWith("--")).ToArray());

            case "probe":
                return ProbeCommand.Run(devices, args.Skip(1).Where(a => !a.StartsWith("--")).ToArray());

            case "setdefault":
                return SetDefaultCommand.Run(devices, args.Skip(1).Where(a => !a.StartsWith("--")).ToArray());

            case "run":
                return RunCommand.Run(devices, args.Skip(1).Where(a => !a.StartsWith("--")).ToArray());

            case "dump":
                foreach (var line in devices.DumpProperties())
                {
                    Console.WriteLine(line);
                }
                return 0;

            default:
                Console.WriteLine("Usage: Audora.Cli [list|watch|dump|mirror|probe|setdefault|run] [--all]");
                return 1;
        }
    }

    private static void PrintList(DeviceEnumerator devices, bool showAll)
    {
        var all = devices.GetRenderDevices();
        var shown = showAll ? all : all.Where(d => d.Status == DeviceStatus.Active).ToList();

        Console.WriteLine($"  {"Name",-52} {"Type",-10} {"Status",-11}");
        foreach (var d in shown)
        {
            var marker = d.IsDefault ? "*" : " ";
            Console.WriteLine($"{marker} {d.Name,-52} {d.ConnectionType,-10} {d.Status,-11}");
        }

        Console.WriteLine($"  ({shown.Count} shown, {all.Count} known; * = Windows default output)");
        if (!showAll)
        {
            Console.WriteLine("  Add --all to include disconnected and remembered devices.");
        }
    }

    private static int Watch(DeviceEnumerator devices, bool showAll)
    {
        devices.DevicesChanged += (_, _) =>
        {
            Console.WriteLine();
            Console.WriteLine($"--- change detected at {DateTime.Now:T} ---");
            PrintList(devices, showAll);
        };

        Console.WriteLine("Watching for device changes. Plug, unplug, connect or disconnect devices.");
        Console.WriteLine("Press Enter to quit.");
        Console.WriteLine();
        PrintList(devices, showAll);
        Console.ReadLine();
        return 0;
    }
}
