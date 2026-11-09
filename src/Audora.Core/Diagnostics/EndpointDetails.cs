using System.Text.RegularExpressions;

namespace Audora.Core.Diagnostics;

/// <summary>The format Windows uses for a device in shared mode.</summary>
public sealed record AudioFormatInfo(int SampleRate, int Bits, int Channels)
{
    public string Describe()
    {
        var channels = Channels switch
        {
            1 => "mono",
            2 => "stereo",
            _ => $"{Channels} channels"
        };
        return $"{SampleRate} Hz, {Bits}-bit, {channels}";
    }
}

/// <summary>Raw values read from one connected device. A value is null when Windows does not store it.</summary>
public sealed record EndpointRaw(
    string Name,
    bool IsOutput,
    bool IsDefault,
    byte[]? DeviceFormat,
    int? DisableEffects,
    string? HardwareInstance,
    string? DriverRecord);

/// <summary>What the scan could work out about one connected device. Null means unknown.</summary>
public sealed record EndpointDetails(
    string Name,
    bool IsOutput,
    bool IsDefault,
    AudioFormatInfo? Format,
    bool? EnhancementsOff,
    string? DriverInf,
    string? DriverVersion,
    int? ProblemCode);

public static class WaveFormatParser
{
    private const int ExtensibleTag = 0xFFFE;

    /// <summary>Reads a WAVEFORMATEX or WAVEFORMATEXTENSIBLE block. Returns null if it is too short or looks wrong.</summary>
    public static AudioFormatInfo? Parse(byte[]? blob)
    {
        if (blob is null || blob.Length < 16)
        {
            return null;
        }

        var tag = BitConverter.ToUInt16(blob, 0);
        var channels = BitConverter.ToUInt16(blob, 2);
        var rate = BitConverter.ToInt32(blob, 4);
        int bits = BitConverter.ToUInt16(blob, 14);

        if (tag == ExtensibleTag && blob.Length >= 20)
        {
            int valid = BitConverter.ToUInt16(blob, 18);
            if (valid > 0)
            {
                bits = valid;
            }
        }

        if (channels == 0 || rate <= 0 || bits == 0)
        {
            return null;
        }

        return new AudioFormatInfo(rate, bits, channels);
    }
}

public static class DriverInfoParser
{
    /// <summary>Splits a driver record like "oem11.inf:hash:Section:6.0.9683.1:HDAUDIO..." into INF name and version.</summary>
    public static (string? Inf, string? Version) Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null);
        }

        var parts = text.Split(':');
        var inf = parts[0].EndsWith(".inf", StringComparison.OrdinalIgnoreCase) ? parts[0] : null;

        string? version = null;
        if (parts.Length >= 4 && parts[3].Length > 0 && parts[3].All(c => char.IsDigit(c) || c == '.'))
        {
            version = parts[3];
        }

        return (inf, version);
    }
}

public static class PnpProblem
{
    /// <summary>Plain words for the Windows Device Manager problem codes people meet most often.</summary>
    public static string Describe(int code) => code switch
    {
        0 => "no problem reported",
        1 => "the device is not set up correctly",
        10 => "the device cannot start",
        14 => "the device needs a restart of Windows",
        18 => "the driver needs to be reinstalled",
        22 => "the device is turned off in Windows",
        24 => "the device is not present or not working",
        28 => "no driver is installed for the device",
        31 => "the device is not working properly",
        37 => "Windows could not start the driver",
        39 => "Windows could not load the driver",
        43 => "Windows stopped the device because it reported a problem",
        52 => "Windows cannot verify the driver's signature",
        _ => $"Windows reports a problem with the device (code {code})"
    };
}

public static class EndpointDetailsReader
{
    /// <summary>
    /// Turns raw values into details. The problem lookup is passed in, so this can be tested without Windows.
    /// </summary>
    public static IReadOnlyList<EndpointDetails> Build(IReadOnlyList<EndpointRaw> raws, Func<string, int?> problemLookup)
    {
        var result = new List<EndpointDetails>();

        foreach (var raw in raws)
        {
            var (inf, version) = DriverInfoParser.Parse(raw.DriverRecord);
            var instance = InstanceFrom(raw.HardwareInstance);
            int? problem = instance is null ? null : problemLookup(instance);

            result.Add(new EndpointDetails(
                raw.Name,
                raw.IsOutput,
                raw.IsDefault,
                WaveFormatParser.Parse(raw.DeviceFormat),
                raw.DisableEffects is null ? null : raw.DisableEffects == 1,
                inf,
                version,
                problem));
        }

        return result;
    }

    /// <summary>The value looks like "{1}.HDAUDIO\FUNC_01&amp;...\5&amp;338D889C&amp;0&amp;0001"; the part after "{n}." is the device instance id.</summary>
    public static string? InstanceFrom(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = Regex.Replace(value, @"^\{\d+\}\.", string.Empty);
        return text.Length == 0 ? null : text;
    }
}
