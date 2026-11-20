using System.Runtime.InteropServices;
using Audora.Core.Audio;

namespace Audora.Tests;

public class MicrophoneSessionTests
{
    [Fact]
    public void ASessionNeedsAMicrophoneId()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MicrophoneSession(" "));
    }

    [Fact]
    public void SettingsAreRememberedBeforeTheMicrophoneStarts()
    {
        using var session = new MicrophoneSession("some-id");

        session.NoiseRemoval = true;
        session.Strength = 0.4f;

        Assert.True(session.NoiseRemoval);
        Assert.Equal(0.4, (double)session.Strength, 5);
        Assert.False(session.IsRunning);
    }

    [Fact]
    public void StrengthIsKeptBetweenZeroAndOne()
    {
        using var session = new MicrophoneSession("some-id");

        session.Strength = 5f;
        Assert.Equal(1.0, (double)session.Strength, 5);

        session.Strength = -2f;
        Assert.Equal(0.0, (double)session.Strength, 5);

        session.Strength = float.NaN;
        Assert.Equal(1.0, (double)session.Strength, 5);
    }

    [Fact]
    public void LevelsAreZeroWhileNothingIsRunning()
    {
        using var session = new MicrophoneSession("some-id");

        var levels = session.TakeLevels();

        Assert.Equal(0.0, (double)levels.InputPeak, 5);
        Assert.Equal(0.0, (double)levels.OutputRms, 5);
        Assert.False(session.NoiseRemovalFailed);
    }

    [Fact]
    public void AMicrophoneThatDoesNotExistSaysWhyInsteadOfThrowing()
    {
        using var session = new MicrophoneSession("not-a-real-microphone");

        var error = session.Start();

        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.False(session.IsRunning);
    }

    [Fact]
    public void AClosedSessionDoesNotStart()
    {
        var session = new MicrophoneSession("some-id");
        session.Dispose();
        session.Dispose();

        Assert.Contains("closed", session.Start());
    }

    [Fact]
    public void AccessDeniedPointsAtThePrivacySetting()
    {
        var message = MicrophoneSession.DescribeStartError(new UnauthorizedAccessException());
        var com = MicrophoneSession.DescribeStartError(new COMException("denied", unchecked((int)0x80070005)));

        Assert.Contains("privacy", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("privacy", com, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OtherProblemsKeepTheirOwnMessage()
    {
        Assert.Equal("It broke.", MicrophoneSession.DescribeStartError(new InvalidOperationException("It broke.")));
    }
}
