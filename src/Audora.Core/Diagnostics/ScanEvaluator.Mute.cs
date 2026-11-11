using Audora.Core.Devices;

namespace Audora.Core.Diagnostics;

public static partial class ScanEvaluator
{
    public const string RepairUnmute = "unmute";
    public const string RepairOpenMonoSettings = "open-mono-settings";

    private static void CheckMuted(ScanInput input, List<ScanFinding> findings)
    {
        var muted = input.MutedDeviceIds;
        if (muted is null || muted.Count == 0)
        {
            return;
        }

        AddMuted(input.RenderDevices, OutputsArea, muted, findings);
        AddMuted(input.CaptureDevices, InputsArea, muted, findings);
    }

    private static void AddMuted(
        IReadOnlyList<AudioDeviceInfo> devices,
        string area,
        IReadOnlyCollection<string> muted,
        List<ScanFinding> findings)
    {
        foreach (var device in devices)
        {
            if (device.Status != DeviceStatus.Active || !muted.Contains(device.Id))
            {
                continue;
            }

            findings.Add(new ScanFinding(
                device.IsDefault ? ScanLevel.Warning : ScanLevel.Info,
                area,
                $"{device.Name} is muted in Windows",
                device.IsDefault
                    ? "This is your default device, so you will hear nothing from it. Muting is a Windows setting."
                    : "A muted device plays no sound. This is a Windows setting, and it may be on purpose.",
                RepairUnmute,
                device.Id,
                device.Name));
        }
    }
}
