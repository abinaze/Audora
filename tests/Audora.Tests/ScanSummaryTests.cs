using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ScanSummaryTests
{
    private static ScanReport Report(params ScanLevel[] levels)
    {
        var findings = levels.Select(l => new ScanFinding(l, "Area", "Title", string.Empty)).ToList();
        var input = new ScanInput(
            Array.Empty<ServiceSnapshot>(),
            Array.Empty<AudioDeviceInfo>(),
            Array.Empty<AudioDeviceInfo>());
        return new ScanReport(DateTime.Now, input, findings);
    }

    [Fact]
    public void CleanScanSaysNoProblemsAndCountsNotes()
    {
        var report = Report(ScanLevel.Ok, ScanLevel.Info, ScanLevel.Info, ScanLevel.Info);

        Assert.Equal("No problems found, 3 notes.", ScanSummary.Describe(report));
    }

    [Fact]
    public void ProblemsComeFirstThenWarningsAndNotes()
    {
        var report = Report(ScanLevel.Problem, ScanLevel.Warning, ScanLevel.Warning, ScanLevel.Info);

        Assert.Equal("1 problem found, 2 warnings, 1 note.", ScanSummary.Describe(report));
    }

    [Fact]
    public void WarningsAloneAreNamedAsWarnings()
    {
        var report = Report(ScanLevel.Warning);

        Assert.Equal("1 warning found.", ScanSummary.Describe(report));
    }
}
