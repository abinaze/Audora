using Audora.Core.Devices;

namespace Audora.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
        using var devices = new DeviceEnumerator();

        switch (command)
        {
            case "list":
                PrintList(devices);
                return 0;

            case "watch":
                return Watch(devices);

            case "dump":
                foreach (var line in devices.DumpProperties())
                {
                    Console.WriteLine(line);
                }
                return 0;

            default:
                Console.WriteLine("Usage: Audora.Cli [list|watch|dump]");
                return 1;
        }
    }

    private static void PrintList(DeviceEnumerator devices)
    {
        Console.WriteLine($"  {"Name",-52} {"Type",-10} {"Status",-11}");
        foreach (var d in devices.GetRenderDevices())
        {
            var marker = d.IsDefault ? "*" : " ";
            Console.WriteLine($"{marker} {d.Name,-52} {d.ConnectionType,-10} {d.Status,-11}");
        }
        Console.WriteLine("  (* = Windows default output)");
    }

    private static int Watch(DeviceEnumerator devices)
    {
        devices.DevicesChanged += (_, _) =>
        {
            Console.WriteLine();
            Console.WriteLine($"--- change detected at {DateTime.Now:T} ---");
            PrintList(devices);
        };

        Console.WriteLine("Watching for device changes. Plug, unplug, connect or disconnect devices.");
        Console.WriteLine("Press Enter to quit.");
        Console.WriteLine();
        PrintList(devices);
        Console.ReadLine();
        return 0;
    }
}
