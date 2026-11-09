namespace Audora.Core.Diagnostics;

public static partial class ScanEvaluator
{
    public const string DriversArea = "Drivers and formats";

    private const int VeryHighSampleRate = 96000;

    private static void CheckEndpointDetails(IReadOnlyList<EndpointDetails>? details, List<ScanFinding> findings)
    {
        if (details is null)
        {
            return;
        }

        foreach (var d in details)
        {
            findings.Add(DriverFinding(d));

            if (d.Format is not null && d.Format.SampleRate > VeryHighSampleRate)
            {
                findings.Add(new ScanFinding(ScanLevel.Info, DriversArea,
                    $"{d.Name} runs at a very high sample rate ({d.Format.SampleRate} Hz)",
                    "Rates this high are rarely needed. If this device crackles, a lower rate such as 48000 Hz is the usual test. Audora does not change it."));
            }

            if (d.EnhancementsOff == true)
            {
                findings.Add(new ScanFinding(ScanLevel.Info, DriversArea,
                    $"{d.Name}: Windows sound enhancements are turned off",
                    "This is a setting in Windows. Audora does not change it."));
            }
        }
    }

    private static ScanFinding DriverFinding(EndpointDetails d) => d.ProblemCode switch
    {
        null => new ScanFinding(ScanLevel.Info, DriversArea,
            $"{d.Name}: driver status could not be read{Summary(d)}",
            "Windows did not let the scan look up this device's driver state. This is not a fault by itself."),
        0 => new ScanFinding(ScanLevel.Ok, DriversArea,
            $"{d.Name}: no driver problem{Summary(d)}",
            string.Empty),
        int code => new ScanFinding(ScanLevel.Problem, DriversArea,
            $"{d.Name}: {PnpProblem.Describe(code)}{Summary(d)}",
            "A driver update or a restart may help. Audora changes nothing without your approval.")
    };

    private static string Summary(EndpointDetails d)
    {
        var parts = new List<string>();

        if (d.DriverInf is not null)
        {
            parts.Add(d.DriverVersion is null ? d.DriverInf : $"{d.DriverInf} {d.DriverVersion}");
        }

        if (d.Format is not null)
        {
            parts.Add(d.Format.Describe());
        }

        return parts.Count == 0 ? string.Empty : $" ({string.Join(", ", parts)})";
    }
}
