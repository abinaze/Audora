using Audora.Core.Audio;

namespace Audora.Tests;

public class CallGateTests
{
    [Fact]
    public void ItStartsWithNoCall()
    {
        Assert.False(new CallGate().Active);
    }

    [Fact]
    public void ItNeedsTwoChecksInARowToTurnOn()
    {
        var gate = new CallGate();

        Assert.False(gate.Observe(true));
        Assert.True(gate.Observe(true));
    }

    [Fact]
    public void ItNeedsTwoChecksInARowToTurnOff()
    {
        var gate = new CallGate();
        gate.Observe(true);
        gate.Observe(true);

        Assert.True(gate.Observe(false));
        Assert.False(gate.Observe(false));
    }

    [Fact]
    public void OneOddCheckDoesNotFlipIt()
    {
        var gate = new CallGate();
        gate.Observe(true);
        gate.Observe(true);

        gate.Observe(false);
        Assert.True(gate.Observe(true));
        Assert.True(gate.Observe(false));
    }

    [Fact]
    public void ACheckThatCannotTellStartsTheCountAgain()
    {
        var gate = new CallGate();
        gate.Observe(true);
        gate.Observe(true);

        gate.Observe(false);
        gate.Observe(null);

        Assert.True(gate.Observe(false));
        Assert.False(gate.Observe(false));
    }

    [Fact]
    public void ResetTurnsItOff()
    {
        var gate = new CallGate();
        gate.Observe(true);
        gate.Observe(true);

        gate.Reset();

        Assert.False(gate.Active);
        Assert.False(gate.Observe(true));
    }

    [Fact]
    public void ItRecognisesCallAppsAndNothingElse()
    {
        Assert.True(CallGate.IsCall("Teams", "Standup"));
        Assert.True(CallGate.IsCall("ms-teams", string.Empty));
        Assert.True(CallGate.IsCall("chrome", "Google Meet - Standup"));
        Assert.False(CallGate.IsCall("spotify", "Some song"));
        Assert.False(CallGate.IsCall("chrome", "YouTube"));
        Assert.False(CallGate.IsCall(null, null));
    }
}
