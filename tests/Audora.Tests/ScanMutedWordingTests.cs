using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ScanMutedWordingTests
{
    [Fact]
    public void MutedMicrophoneIsDescribedAsNotRecording()
    {
        var mic = new AudioDeviceInfo("id-Mic", "Mic", ConnectionType.Unknown, DeviceStatus.Active, true);

        var findings = ScanEvaluator.Evaluate(new ScanInput(
                Array.Empty<ServiceSnapshot>(),
                Array.Empty<AudioDeviceInfo>(),
                new[] { mic },
                null,
                null,
                new[] { "id-Mic" }))
            .Where(f => f.Title.Contains("muted in Windows"))
            .ToList();

        var finding = Assert.Single(findings);
        Assert.Contains("records no sound", finding.Detail);
    }
}
