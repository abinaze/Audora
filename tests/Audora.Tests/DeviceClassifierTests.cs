using Audora.Core.Devices;

namespace Audora.Tests;

public class DeviceClassifierTests
{
    [Theory]
    [InlineData(3, "Headphones (WH-1000XM4)", @"{1}.BTHENUM\DEV_ABC123", ConnectionType.Bluetooth)]
    [InlineData(5, "Headset (Buds Hands-Free AG Audio)", "", ConnectionType.Bluetooth)]
    [InlineData(3, "Headphones (USB Audio Device)", @"USB\VID_0D8C&PID_0014", ConnectionType.Usb)]
    [InlineData(9, "LG ULTRAGEAR (NVIDIA High Definition Audio)", @"HDAUDIO\FUNC_01", ConnectionType.Hdmi)]
    [InlineData(1, "Speakers (Realtek(R) Audio)", @"HDAUDIO\FUNC_01&VEN_10EC", ConnectionType.Analog)]
    [InlineData(8, "Digital Output (S/PDIF)", "", ConnectionType.Other)]
    [InlineData(null, "Mystery Device", "", ConnectionType.Unknown)]
    public void Classify_ReturnsExpectedType(int? formFactor, string name, string hint, ConnectionType expected)
    {
        var result = DeviceClassifier.Classify(formFactor, name, new[] { hint });
        Assert.Equal(expected, result);
    }
}
