using Audora.Core.Devices;

namespace Audora.Cli;

internal static class SetDefaultCommand
{
    public static int Run(DeviceEnumerator devices, string[] terms)
    {
        var term = string.Join(' ', terms).Trim();
        var active = devices.GetActiveRenderDevices().ToList();

        if (term.Length == 0)
        {
            Console.WriteLine("Usage: Audora.Cli setdefault <part of device name>");
            Console.WriteLine("Active devices:");
            foreach (var d in active)
            {
                Console.WriteLine($"{(d.IsDefault ? "*" : " ")} {d.Name}");
            }
            return 1;
        }

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

        var target = matches[0];
        if (target.IsDefault)
        {
            Console.WriteLine($"{target.Name} is already the default output.");
            return 0;
        }

        DefaultDeviceSwitcher.SetDefaultOutput(target.Id);
        Thread.Sleep(400);

        var now = devices.GetRenderDevices().FirstOrDefault(d => d.IsDefault);
        Console.WriteLine($"Default output is now: {now?.Name ?? "(none)"}");
        return now is not null && now.Id == target.Id ? 0 : 1;
    }
}
