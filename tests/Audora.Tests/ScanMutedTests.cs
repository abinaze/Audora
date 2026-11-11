using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ScanMutedTests
{
    private static AudioDeviceInfo Dev(string name, bool isDefault, DeviceStatus status = DeviceStatus.Active) =>
        new("id-" + name, name, ConnectionType.Unknown, status, isDefault);

    private static IReadOnlyList<ScanFinding> Run(AudioDeviceInfo[] outputs, params string[] mutedIds) =>
        ScanEvaluator.Evaluate(new ScanInput(
                Array.Empty<ServiceSnapshot>(),
                outputs,
                Array.Empty<AudioDeviceInfo>(),
                null,
                null,
                mutedIds))
            .Where(f => f.Title.Contains("muted in Windows"))
            .ToList();

    [Fact]
    public void MutedDefaultDeviceIsAWarningWithAnUnmuteFix()
    {
        var findings = Run(new[] { Dev("Speakers", true) }, "id-Speakers");

        var finding = Assert.Single(findings);
        Assert.Equal(ScanLevel.Warning, finding.Level);
        Assert.Equal(ScanEvaluator.RepairUnmute, finding.RepairKey);
        Assert.Equal("id-Speakers", finding.DeviceId);
        Assert.Equal("Speakers", finding.DeviceName);
    }

    [Fact]
    public void MutedOtherDeviceIsOnlyANote()
    {
        var findings = Run(new[] { Dev("Speakers", true), Dev("USB", false) }, "id-USB");

        var finding = Assert.Single(findings);
        Assert.Equal(ScanLevel.Info, finding.Level);
    }

    [Fact]
    public void UnmutedOrDisconnectedDevicesAreIgnored()
    {
        var outputs = new[] { Dev("Speakers", true), Dev("Old", false, DeviceStatus.Unplugged) };

        Assert.Empty(Run(outputs, "id-Old"));
    }
}
