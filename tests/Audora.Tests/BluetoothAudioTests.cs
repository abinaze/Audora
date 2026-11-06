using Audora.Core.Devices;

namespace Audora.Tests;

public class BluetoothAudioTests
{
    [Theory]
    [InlineData(0x240404u, true)]    // headphones
    [InlineData(0x240414u, true)]    // loudspeaker
    [InlineData(0x240418u, true)]    // headset
    [InlineData(0x5A020Cu, false)]   // phone
    [InlineData(0x000104u, false)]   // computer
    public void OnlyAudioDevicesAreListed(uint classOfDevice, bool expected) =>
        Assert.Equal(expected, BluetoothAudio.IsAudioClass(classOfDevice));

    [Fact]
    public void ListingNeverThrows_EvenWithoutBluetooth() =>
        Assert.NotNull(BluetoothAudio.ListPaired());
}
