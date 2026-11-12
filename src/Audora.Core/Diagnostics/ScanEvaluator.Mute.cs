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
                MutedDetail(area == InputsArea, device.IsDefault),
                RepairUnmute,
                device.Id,
                device.Name));
        }
    }

    private static string MutedDetail(bool isInput, bool isDefault) => (isInput, isDefault) switch
    {
        (true, true) => "This is the default microphone. It records no sound until it is unmuted.",
        (true, false) => "A muted microphone records no sound. This is a Windows setting and may be intentional.",
        (false, true) => "This is the default output. It plays no sound until it is unmuted.",
        _ => "A muted output plays no sound. This is a Windows setting and may be intentional."
    };
}
