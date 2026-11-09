using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ScanEvaluatorTests
{
    private static ServiceSnapshot Svc(
        ServiceRunState state,
        ServiceStartup startup = ServiceStartup.Automatic,
        bool found = true) =>
        new("Audiosrv", "Windows Audio", found, state, startup);

    private static AudioDeviceInfo Dev(string name, DeviceStatus status = DeviceStatus.Active, bool isDefault = false) =>
        new("id-" + name, name, ConnectionType.Unknown, status, isDefault);

    private static IReadOnlyList<ScanFinding> Run(
        ServiceSnapshot service,
        IReadOnlyList<AudioDeviceInfo> outputs,
        IReadOnlyList<AudioDeviceInfo> inputs) =>
        ScanEvaluator.Evaluate(new ScanInput(new[] { service }, outputs, inputs));

    private static readonly AudioDeviceInfo[] OneSpeaker = { Dev("Speakers", isDefault: true) };
    private static readonly AudioDeviceInfo[] OneMic = { Dev("Microphone", isDefault: true) };

    [Fact]
    public void RunningServiceIsGoodNews()
    {
        var findings = Run(Svc(ServiceRunState.Running), OneSpeaker, OneMic);

        Assert.Contains(findings, f => f.Area == ScanEvaluator.ServicesArea && f.Level == ScanLevel.Ok);
    }

    [Fact]
    public void StoppedServiceIsAProblem()
    {
        var findings = Run(Svc(ServiceRunState.Stopped), OneSpeaker, OneMic);

        Assert.Contains(findings, f => f.Area == ScanEvaluator.ServicesArea && f.Level == ScanLevel.Problem);
    }

    [Fact]
    public void DisabledServiceIsAProblemEvenWhenItIsRunning()
    {
        var findings = Run(Svc(ServiceRunState.Running, ServiceStartup.Disabled), OneSpeaker, OneMic);

        Assert.Contains(findings, f => f.Area == ScanEvaluator.ServicesArea && f.Level == ScanLevel.Problem);
    }

    [Fact]
    public void MissingServiceIsAProblem()
    {
        var findings = Run(Svc(ServiceRunState.Unknown, found: false), OneSpeaker, OneMic);

        Assert.Contains(findings, f => f.Area == ScanEvaluator.ServicesArea && f.Level == ScanLevel.Problem);
    }

    [Fact]
    public void NoActiveOutputIsAProblem()
    {
        var findings = Run(Svc(ServiceRunState.Running), new[] { Dev("Old Buds", DeviceStatus.Unplugged) }, OneMic);

        Assert.Contains(findings, f => f.Area == ScanEvaluator.OutputsArea && f.Level == ScanLevel.Problem);
    }

    [Fact]
    public void HealthyPcHasNoWarningsOrProblems()
    {
        var findings = Run(Svc(ServiceRunState.Running), OneSpeaker, OneMic);

        Assert.DoesNotContain(findings, f => f.Level >= ScanLevel.Warning);
    }

    [Fact]
    public void NoMicrophoneIsOnlyANote()
    {
        var findings = Run(Svc(ServiceRunState.Running), OneSpeaker, Array.Empty<AudioDeviceInfo>());

        Assert.Contains(findings, f => f.Area == ScanEvaluator.InputsArea && f.Level == ScanLevel.Info);
        Assert.DoesNotContain(findings, f => f.Area == ScanEvaluator.InputsArea && f.Level >= ScanLevel.Warning);
    }

    [Fact]
    public void TurnedOffOutputIsOnlyANote()
    {
        var outputs = new[] { Dev("Speakers", isDefault: true), Dev("HDMI", DeviceStatus.Disabled) };

        var findings = Run(Svc(ServiceRunState.Running), outputs, OneMic);

        Assert.Contains(findings, f => f.Area == ScanEvaluator.OutputsArea && f.Level == ScanLevel.Info);
        Assert.DoesNotContain(findings, f => f.Level >= ScanLevel.Warning);
    }

    [Fact]
    public void ActiveOutputsWithoutADefaultGiveAWarning()
    {
        var findings = Run(Svc(ServiceRunState.Running), new[] { Dev("Speakers") }, OneMic);

        Assert.Contains(findings, f => f.Area == ScanEvaluator.OutputsArea && f.Level == ScanLevel.Warning);
    }

    [Fact]
    public void WorstLevelFollowsTheFindings()
    {
        var input = new ScanInput(new[] { Svc(ServiceRunState.Stopped) }, OneSpeaker, OneMic);
        var report = new ScanReport(DateTime.Now, input, ScanEvaluator.Evaluate(input));

        Assert.Equal(ScanLevel.Problem, report.Worst);
        Assert.Equal(1, report.Count(ScanLevel.Problem));
    }
}
