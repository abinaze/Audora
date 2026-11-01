namespace Audora.Core.Devices;

/// <summary>
/// Works out how a device is connected from the signals Windows exposes:
/// endpoint form factor, hardware identifier strings and the device name.
/// </summary>
public static class DeviceClassifier
{
    // Values of PKEY_AudioEndpoint_FormFactor
    private const int FormRemoteNetwork = 0;
    private const int FormSpeakers = 1;
    private const int FormLineLevel = 2;
    private const int FormHeadphones = 3;
    private const int FormHeadset = 5;
    private const int FormHandset = 6;
    private const int FormDigitalPassthrough = 7;
    private const int FormSpdif = 8;
    private const int FormDisplay = 9;

    public static ConnectionType Classify(int? formFactor, string? name, IEnumerable<string> hardwareHints)
    {
        var hints = string.Join("|", hardwareHints).ToUpperInvariant();
        var label = (name ?? string.Empty).ToUpperInvariant();

        if (Has(hints, "BTHENUM", "BTHHFENUM", "BTHLE", "BTHA2DP", "BLUETOOTH")
            || Has(label, "BLUETOOTH", "HANDS-FREE"))
        {
            return ConnectionType.Bluetooth;
        }

        if (Has(hints, @"USB\") || Has(label, "USB"))
        {
            return ConnectionType.Usb;
        }

        if (formFactor == FormDisplay
            || Has(hints, "HDMI", "DISPLAYPORT")
            || Has(label, "HDMI", "DISPLAYPORT", "DISPLAY AUDIO", "NVIDIA HIGH DEFINITION", "AMD HIGH DEFINITION"))
        {
            return ConnectionType.Hdmi;
        }

        if (formFactor is FormSpeakers or FormLineLevel or FormHeadphones or FormHeadset or FormHandset
            || Has(hints, "HDAUDIO", "INTELAUDIO"))
        {
            return ConnectionType.Analog;
        }

        if (formFactor is FormRemoteNetwork or FormDigitalPassthrough or FormSpdif)
        {
            return ConnectionType.Other;
        }

        return ConnectionType.Unknown;
    }

    private static bool Has(string text, params string[] tokens) =>
        tokens.Any(t => text.Contains(t, StringComparison.Ordinal));
}
