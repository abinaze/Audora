using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ScanMonoAudioTests
{
    private static IReadOnlyList<ScanFinding> Run(bool? mono) =>
        ScanEvaluator.Evaluate(new ScanInput(
                Array.Empty<ServiceSnapshot>(),
                Array.Empty<AudioDeviceInfo>(),
                Array.Empty<AudioDeviceInfo>(),
                null,
                mono))
            .Where(f => f.Area == ScanEvaluator.SettingsArea)
            .ToList();

    [Fact]
    public void MonoAudioOnIsAWarning()
    {
        var findings = Run(true);

        Assert.Contains(findings, f => f.Level == ScanLevel.Warning && f.Title.Contains("Mono audio is on"));
    }

    [Fact]
    public void MonoAudioOffIsGoodNews()
    {
        var findings = Run(false);

        Assert.Contains(findings, f => f.Level == ScanLevel.Ok);
    }

    [Fact]
    public void UnknownMonoAudioSaysNothing()
    {
        Assert.Empty(Run(null));
    }
}
