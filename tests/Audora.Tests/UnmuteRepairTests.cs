using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class UnmuteRepairTests
{
    private sealed class FakeAccess : IMuteAccess
    {
        public bool? Muted;
        public bool ThrowOnSet;
        public bool IgnoreSet;
        public List<bool> Sets { get; } = new();

        public bool? GetMuted(string deviceId) => Muted;

        public void SetMuted(string deviceId, bool muted)
        {
            if (ThrowOnSet)
            {
                throw new InvalidOperationException();
            }

            Sets.Add(muted);
            if (!IgnoreSet)
            {
                Muted = muted;
            }
        }
    }

    [Fact]
    public void MutedDeviceIsUnmutedAndChecked()
    {
        var access = new FakeAccess { Muted = true };

        var result = new UnmuteRepair(access).Apply("id", "Speakers");

        Assert.True(result.Success);
        Assert.True(result.PreviousMuted);
        Assert.Equal(new[] { false }, access.Sets);
        Assert.False(access.Muted);
    }

    [Fact]
    public void AlreadyUnmutedDeviceIsLeftAlone()
    {
        var access = new FakeAccess { Muted = false };

        var result = new UnmuteRepair(access).Apply("id", "Speakers");

        Assert.True(result.Success);
        Assert.Empty(access.Sets);
    }

    [Fact]
    public void UnreadableMuteStateChangesNothing()
    {
        var access = new FakeAccess { Muted = null };

        var result = new UnmuteRepair(access).Apply("id", "Speakers");

        Assert.False(result.Success);
        Assert.Empty(access.Sets);
    }

    [Fact]
    public void RefusedChangeIsReportedAsAFailure()
    {
        var access = new FakeAccess { Muted = true, ThrowOnSet = true };

        var result = new UnmuteRepair(access).Apply("id", "Speakers");

        Assert.False(result.Success);
        Assert.Contains("Nothing was changed", result.Message);
    }

    [Fact]
    public void StillMutedAfterwardsIsNeverReportedAsFixed()
    {
        var access = new FakeAccess { Muted = true, IgnoreSet = true };

        var result = new UnmuteRepair(access).Apply("id", "Speakers");

        Assert.False(result.Success);
        Assert.Contains("still reports", result.Message);
    }

    [Fact]
    public void UndoPutsTheMuteBack()
    {
        var access = new FakeAccess { Muted = true };
        var repair = new UnmuteRepair(access);
        repair.Apply("id", "Speakers");

        var undone = repair.Undo("id", true);

        Assert.True(undone);
        Assert.True(access.Muted);
    }
}
