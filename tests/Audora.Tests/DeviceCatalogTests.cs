using Audora.Core.Devices;

namespace Audora.Tests;

public class DeviceCatalogTests
{
    private static AudioDeviceInfo Dev(string name, DeviceStatus status = DeviceStatus.Active) =>
        new("id-" + name, name, ConnectionType.Unknown, status, false);

    [Fact]
    public void OnlyActiveDevicesAreShown()
    {
        var shown = DeviceCatalog.Visible(new[]
        {
            Dev("Speakers (Realtek(R) Audio)"),
            Dev("Headphones (Old Buds)", DeviceStatus.Unplugged),
            Dev("Digital Audio (HDMI)", DeviceStatus.NotPresent),
        });

        Assert.Equal(new[] { "Speakers (Realtek(R) Audio)" }, shown.Select(d => d.Name).ToArray());
    }

    [Fact]
    public void HandsFreeEntryIsHiddenWhenTheStereoEntryExists()
    {
        var shown = DeviceCatalog.Visible(new[]
        {
            Dev("Headset (MT Hands-Free)"),
            Dev("Headphones (MT)"),
        });

        Assert.Equal(new[] { "Headphones (MT)" }, shown.Select(d => d.Name).ToArray());
    }

    [Fact]
    public void HandsFreeEntryIsKeptWhenItIsTheOnlyOne()
    {
        var shown = DeviceCatalog.Visible(new[] { Dev("Headset (MT Hands-Free)") });
        Assert.Single(shown);
    }

    [Fact]
    public void DifferentBluetoothDevicesAreNotMerged()
    {
        var shown = DeviceCatalog.Visible(new[]
        {
            Dev("Headset (OPPO Enco Air3 Pro Hands-Free)"),
            Dev("Headphones (MT)"),
        });

        Assert.Equal(2, shown.Count);
    }

    [Theory]
    [InlineData("Headset (MT Hands-Free)", "MT")]
    [InlineData("Headset (OPPO Enco Air3 Pro Hands-Free AG Audio)", "OPPO Enco Air3 Pro")]
    [InlineData("Headphones (Realtek(R) Audio)", "Realtek(R) Audio")]
    [InlineData("Speakers", "Speakers")]
    public void BaseNameStripsTheCallProfileSuffix(string name, string expected) =>
        Assert.Equal(expected, DeviceCatalog.BaseName(name));
}
