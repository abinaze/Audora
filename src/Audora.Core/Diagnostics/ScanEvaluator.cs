using Audora.Core.Devices;

namespace Audora.Core.Diagnostics;

/// <summary>
/// Turns what the scan read into findings. It only describes; it never changes anything.
/// A missing microphone is only a note, because many PCs have none.
/// </summary>
public static partial class ScanEvaluator
{
    public const string ServicesArea = "Windows audio services";
    public const string OutputsArea = "Sound outputs";
    public const string InputsArea = "Microphones";

    public static IReadOnlyList<ScanFinding> Evaluate(ScanInput input)
    {
        var findings = new List<ScanFinding>();

        foreach (var service in input.Services)
        {
            findings.Add(CheckService(service));
        }

        CheckDevices(input.RenderDevices, OutputsArea, "sound output", "sound outputs", required: true, findings);
        CheckDevices(input.CaptureDevices, InputsArea, "microphone", "microphones", required: false, findings);
        CheckEndpointDetails(input.Details, findings);
        CheckMonoAudio(input.MonoAudioOn, findings);
        CheckMuted(input, findings);
        return findings;
    }

    private static ScanFinding CheckService(ServiceSnapshot s)
    {
        if (!s.Found)
        {
            return new ScanFinding(ScanLevel.Problem, ServicesArea, $"{s.DisplayName} was not found",
                "This service is not installed. Windows audio normally requires it.");
        }

        if (s.Startup == ServiceStartup.Disabled)
        {
            return new ScanFinding(ScanLevel.Problem, ServicesArea, $"{s.DisplayName} is disabled",
                "The service is disabled and cannot start. Audora does not change this.");
        }

        return s.State switch
        {
            ServiceRunState.Running => new ScanFinding(ScanLevel.Ok, ServicesArea, $"{s.DisplayName} is running", string.Empty),
            ServiceRunState.StartPending => new ScanFinding(ScanLevel.Info, ServicesArea, $"{s.DisplayName} is starting",
                "Wait a few seconds and scan again."),
            ServiceRunState.Stopped => new ScanFinding(ScanLevel.Problem, ServicesArea, $"{s.DisplayName} is stopped",
                "Windows audio cannot work while this service is stopped."),
            _ => new ScanFinding(ScanLevel.Warning, ServicesArea, $"{s.DisplayName}: the state could not be read",
                "The service state could not be determined.")
        };
    }

    private static void CheckDevices(
        IReadOnlyList<AudioDeviceInfo> devices,
        string area,
        string singular,
        string plural,
        bool required,
        List<ScanFinding> findings)
    {
        var active = devices.Where(d => d.Status == DeviceStatus.Active).ToList();

        if (active.Count == 0)
        {
            findings.Add(required
                ? new ScanFinding(ScanLevel.Problem, area, "No sound output is connected",
                    "No working playback device is reported. Check the cable or Bluetooth connection and the Windows Sound settings.")
                : new ScanFinding(ScanLevel.Info, area, "No microphone is connected",
                    "No working input device is reported. This is normal on a PC without a microphone."));
        }
        else
        {
            findings.Add(new ScanFinding(ScanLevel.Ok, area,
                $"{active.Count} {Noun(active.Count, singular, plural)} connected", Names(active)));

            if (!active.Any(d => d.IsDefault))
            {
                findings.Add(new ScanFinding(ScanLevel.Warning, area, $"No default {singular} is set",
                    "No default device is set. Choose one in the Windows Sound settings."));
            }
        }

        var disabled = devices.Where(d => d.Status == DeviceStatus.Disabled).ToList();
        if (disabled.Count > 0)
        {
            findings.Add(new ScanFinding(ScanLevel.Info, area,
                $"{disabled.Count} {Noun(disabled.Count, singular, plural)} turned off in Windows",
                $"{Names(disabled)}. Windows hides turned-off devices. Audora does not change this."));
        }

        var unplugged = devices.Where(d => d.Status == DeviceStatus.Unplugged).ToList();
        if (unplugged.Count > 0)
        {
            findings.Add(new ScanFinding(ScanLevel.Info, area,
                $"{unplugged.Count} remembered {Noun(unplugged.Count, singular, plural)} not connected",
                Names(unplugged)));
        }

        var remembered = devices.Count(d => d.Status == DeviceStatus.NotPresent);
        if (remembered > 0)
        {
            findings.Add(new ScanFinding(ScanLevel.Info, area,
                $"Windows keeps {remembered} old {Noun(remembered, singular, plural)} on record",
                "Devices connected in the past. Windows keeps these records so settings return when a device reconnects. Audora does not remove them."));
        }
    }

    private static string Noun(int count, string singular, string plural) => count == 1 ? singular : plural;

    private static string Names(IReadOnlyList<AudioDeviceInfo> devices)
    {
        var text = string.Join(", ", devices.Take(3).Select(d => d.Name));
        return devices.Count > 3 ? $"{text} and {devices.Count - 3} more" : text;
    }
}
