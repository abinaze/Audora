using Audora.Core.Audio;

namespace Audora.Tests;

public class DeviceSettingsTests
{
    [Fact]
    public void NoiseRemovalIsOffByDefault()
    {
        Assert.False(new DeviceSettings().NoiseRemoval);
    }

    [Fact]
    public void NoiseRemovalStartsAtFullStrength()
    {
        Assert.Equal(1f, new DeviceSettings().NoiseStrength);
    }
}
