using Audora.Core.Devices;
using Audora.Core.Diagnostics;

namespace Audora.Tests;

public class ScanDetailsTests
{
    private const string RealInstance = @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0256&SUBSYS_104314B3&REV_1000\5&338D889C&0&0001";

    private static byte[] Format(ushort tag, ushort channels, int rate, ushort bits, ushort valid = 0)
    {
        var blob = new byte[valid == 0 ? 16 : 40];
        BitConverter.GetBytes(tag).CopyTo(blob, 0);
        BitConverter.GetBytes(channels).CopyTo(blob, 2);
        BitConverter.GetBytes(rate).CopyTo(blob, 4);
        BitConverter.GetBytes(bits).CopyTo(blob, 14);
        if (valid != 0)
        {
            BitConverter.GetBytes(valid).CopyTo(blob, 18);
        }

        return blob;
    }

    private static EndpointDetails Detail(int? problem = 0, AudioFormatInfo? format = null, bool? enhancementsOff = null) =>
        new("Speakers (Realtek(R) Audio)", true, true, format, enhancementsOff, "oem11.inf", "6.0.9683.1", problem);

    private static IReadOnlyList<ScanFinding> Run(EndpointDetails detail) =>
        ScanEvaluator.Evaluate(new ScanInput(
                Array.Empty<ServiceSnapshot>(),
                Array.Empty<AudioDeviceInfo>(),
                Array.Empty<AudioDeviceInfo>(),
                new[] { detail }))
            .Where(f => f.Area == ScanEvaluator.DriversArea)
            .ToList();

    [Fact]
    public void PlainFormatIsRead()
    {
        var info = WaveFormatParser.Parse(Format(1, 2, 48000, 16));

        Assert.Equal(new AudioFormatInfo(48000, 16, 2), info);
        Assert.Equal("48000 Hz, 16-bit, stereo", info!.Describe());
    }

    [Fact]
    public void ExtensibleFormatUsesTheValidBits()
    {
        var info = WaveFormatParser.Parse(Format(0xFFFE, 2, 48000, 32, 24));

        Assert.Equal(24, info!.Bits);
    }

    [Fact]
    public void ShortOrEmptyFormatIsIgnored()
    {
        Assert.Null(WaveFormatParser.Parse(null));
        Assert.Null(WaveFormatParser.Parse(new byte[4]));
        Assert.Null(WaveFormatParser.Parse(Format(1, 0, 48000, 16)));
    }

    [Fact]
    public void DriverRecordGivesTheInfNameAndVersion()
    {
        var (inf, version) = DriverInfoParser.Parse(
            @"oem11.inf:ed86ca118627db63:IntcAzAudModel.NTamd64:6.0.9683.1:HDAUDIO\FUNC_01&VEN_10EC&DEV_0256&SUBSYS_104314B3");

        Assert.Equal("oem11.inf", inf);
        Assert.Equal("6.0.9683.1", version);
    }

    [Fact]
    public void ShortOrMissingDriverRecordStillWorks()
    {
        Assert.Equal(("oem5.inf", (string?)null), DriverInfoParser.Parse("oem5.inf"));
        Assert.Equal(((string?)null, (string?)null), DriverInfoParser.Parse(null));
    }

    [Fact]
    public void ProblemCodesAreExplainedInPlainWords()
    {
        Assert.Contains("no driver", PnpProblem.Describe(28));
        Assert.Contains("turned off", PnpProblem.Describe(22));
        Assert.Contains("999", PnpProblem.Describe(999));
    }

    [Fact]
    public void InstanceIdIsTakenFromTheDeviceValue()
    {
        string? asked = null;
        var raw = new EndpointRaw("Speakers", true, true, null, null, "{1}." + RealInstance, null);

        var details = EndpointDetailsReader.Build(new[] { raw }, id =>
        {
            asked = id;
            return 0;
        });

        Assert.Equal(RealInstance, asked);
        Assert.Equal(0, details[0].ProblemCode);
    }

    [Fact]
    public void MissingInstanceMeansNoProblemLookup()
    {
        var called = false;
        var raw = new EndpointRaw("Speakers", true, true, null, null, null, null);

        var details = EndpointDetailsReader.Build(new[] { raw }, _ =>
        {
            called = true;
            return 0;
        });

        Assert.False(called);
        Assert.Null(details[0].ProblemCode);
    }

    [Fact]
    public void ZeroProblemCodeIsGoodNews()
    {
        var findings = Run(Detail(0, new AudioFormatInfo(48000, 24, 2)));

        Assert.Contains(findings, f => f.Level == ScanLevel.Ok && f.Title.Contains("no driver problem"));
    }

    [Fact]
    public void ProblemCodeIsAProblem()
    {
        var findings = Run(Detail(28));

        Assert.Contains(findings, f => f.Level == ScanLevel.Problem);
    }

    [Fact]
    public void UnreadableDriverStatusIsOnlyANote()
    {
        var findings = Run(Detail(null));

        Assert.Contains(findings, f => f.Level == ScanLevel.Info);
        Assert.DoesNotContain(findings, f => f.Level >= ScanLevel.Warning);
    }

    [Fact]
    public void VeryHighSampleRateIsANoteNotAWarning()
    {
        var findings = Run(Detail(0, new AudioFormatInfo(192000, 24, 2)));

        Assert.Contains(findings, f => f.Level == ScanLevel.Info && f.Title.Contains("very high"));
        Assert.DoesNotContain(findings, f => f.Level >= ScanLevel.Warning);
    }

    [Fact]
    public void TurnedOffEnhancementsAreOnlyANote()
    {
        var findings = Run(Detail(0, null, enhancementsOff: true));

        Assert.Contains(findings, f => f.Level == ScanLevel.Info && f.Title.Contains("enhancements are turned off"));
    }
}
