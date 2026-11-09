using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ScanRememberedDevicesTests
{
    private static AudioDeviceInfo Dev(string name, DeviceStatus status, bool isDefault = false) =>
        new("id-" + name, name, ConnectionType.Unknown, status, isDefault);

    private static IReadOnlyList<ScanFinding> Run(params AudioDeviceInfo[] outputs) =>
        ScanEvaluator.Evaluate(new ScanInput(
            Array.Empty<ServiceSnapshot>(),
            outputs,
            new[] { Dev("Microphone", DeviceStatus.Active, true) }));

    [Fact]
    public void OldRecordsAreOnlyANoteAndAreCounted()
    {
        var findings = Run(
            Dev("Speakers", DeviceStatus.Active, true),
            Dev("Old one", DeviceStatus.NotPresent),
            Dev("Old two", DeviceStatus.NotPresent));

        Assert.Contains(findings, f => f.Area == ScanEvaluator.OutputsArea
            && f.Level == ScanLevel.Info
            && f.Title.StartsWith("Windows keeps 2"));
        Assert.DoesNotContain(findings, f => f.Level >= ScanLevel.Warning);
    }

    [Fact]
    public void NoOldRecordsMeansNoNote()
    {
        var findings = Run(Dev("Speakers", DeviceStatus.Active, true));

        Assert.DoesNotContain(findings, f => f.Title.StartsWith("Windows keeps"));
    }
}
