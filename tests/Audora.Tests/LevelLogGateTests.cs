using Audora.Core.Audio;

namespace Audora.Tests;

public class LevelLogGateTests
{
    private static readonly DateTime Start = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
    private static CaptureLevel Silent => new("X", 0f, 1);
    private static CaptureLevel Sound => new("X", 0.3f, 1);

    [Fact]
    public void FirstReadingIsLogged()
    {
        Assert.True(new LevelLogGate().ShouldLog(Silent, Start));
    }

    [Fact]
    public void SameStateWithinTheHeartbeatIsNotLogged()
    {
        var gate = new LevelLogGate();
        gate.ShouldLog(Silent, Start);

        Assert.False(gate.ShouldLog(Silent, Start.AddSeconds(15)));
        Assert.False(gate.ShouldLog(Silent, Start.AddMinutes(4)));
    }

    [Fact]
    public void ChangeBetweenSilenceAndSoundIsLogged()
    {
        var gate = new LevelLogGate();
        gate.ShouldLog(Silent, Start);

        Assert.True(gate.ShouldLog(Sound, Start.AddSeconds(15)));
        Assert.True(gate.ShouldLog(Silent, Start.AddSeconds(30)));
    }

    [Fact]
    public void HeartbeatIsLoggedAfterFiveMinutes()
    {
        var gate = new LevelLogGate();
        gate.ShouldLog(Silent, Start);

        Assert.True(gate.ShouldLog(Silent, Start.AddMinutes(5)));
    }

    [Fact]
    public void ResetLogsTheNextReadingAgain()
    {
        var gate = new LevelLogGate();
        gate.ShouldLog(Silent, Start);
        gate.Reset();

        Assert.True(gate.ShouldLog(Silent, Start.AddSeconds(15)));
    }
}
