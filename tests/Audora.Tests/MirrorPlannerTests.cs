using Audora.Core.Audio;
using Audora.Core.Devices;

namespace Audora.Tests;

public class MirrorPlannerTests
{
    private static AudioDeviceInfo Dev(string id, bool isDefault = false, DeviceStatus status = DeviceStatus.Active) =>
        new(id, "Device " + id, ConnectionType.Unknown, status, isDefault);

    [Fact]
    public void SteadyState_ChangesNothing()
    {
        var devices = new[] { Dev("A", true), Dev("B"), Dev("C") };
        var plan = MirrorPlanner.Plan(devices, new[] { "B", "C" }, "A", new[] { "B", "C" });

        Assert.False(plan.RestartCapture);
        Assert.Empty(plan.Remove);
        Assert.Empty(plan.Add);
    }

    [Fact]
    public void DisconnectedTarget_IsRemoved_OthersKeepPlaying()
    {
        var devices = new[] { Dev("A", true), Dev("B", status: DeviceStatus.Unplugged), Dev("C") };
        var plan = MirrorPlanner.Plan(devices, new[] { "B", "C" }, "A", new[] { "B", "C" });

        Assert.False(plan.RestartCapture);
        Assert.Equal(new[] { "B" }, plan.Remove.ToArray());
        Assert.Empty(plan.Add);
    }

    [Fact]
    public void ReconnectedTarget_IsAddedBack()
    {
        var devices = new[] { Dev("A", true), Dev("B"), Dev("C") };
        var plan = MirrorPlanner.Plan(devices, new[] { "B", "C" }, "A", new[] { "C" });

        Assert.False(plan.RestartCapture);
        Assert.Empty(plan.Remove);
        Assert.Equal(new[] { "B" }, plan.Add.ToArray());
    }

    [Fact]
    public void DefaultChange_RestartsCapture_AndNewDefaultIsNotMirrored()
    {
        var devices = new[] { Dev("A"), Dev("B", true), Dev("C") };
        var plan = MirrorPlanner.Plan(devices, new[] { "B", "C" }, "A", new[] { "B", "C" });

        Assert.True(plan.RestartCapture);
        Assert.Equal("B", plan.SourceId);
        Assert.Equal(new[] { "C" }, plan.Add.ToArray());
    }

    [Fact]
    public void DefaultDeviceSelected_IsNeverAMirrorTarget()
    {
        var devices = new[] { Dev("A", true), Dev("B") };
        var plan = MirrorPlanner.Plan(devices, new[] { "A" }, "A", Array.Empty<string>());

        Assert.False(plan.RestartCapture);
        Assert.Empty(plan.Add);
    }

    [Fact]
    public void NoActiveDefault_PausesEverything()
    {
        var devices = new[] { Dev("A", true, DeviceStatus.Unplugged), Dev("B") };
        var plan = MirrorPlanner.Plan(devices, new[] { "B" }, "A", new[] { "B" });

        Assert.Null(plan.SourceId);
        Assert.Equal(new[] { "B" }, plan.Remove.ToArray());
        Assert.Empty(plan.Add);
    }

    [Fact]
    public void ForcedRestart_ReaddsEverySelectedDevice()
    {
        var devices = new[] { Dev("A", true), Dev("B"), Dev("C") };
        var plan = MirrorPlanner.Plan(devices, new[] { "B", "C" }, null, Array.Empty<string>());

        Assert.True(plan.RestartCapture);
        Assert.Equal(new[] { "B", "C" }, plan.Add.ToArray());
    }
}
