namespace Audora.Core.Audio;

/// <summary>
/// What the engine heard since the last check. It tells "Windows sent silence" apart from "Audora failed".
/// </summary>
public sealed record CaptureLevel(string? Source, float Peak, int Outputs)
{
    /// <summary>Below this, the capture counts as silence.</summary>
    public const float SilenceBelow = 0.001f;

    public bool IsSilent => Peak < SilenceBelow;

    public string Describe()
    {
        var where = Source ?? "the default device";
        var outputs = Outputs == 1 ? "1 output" : $"{Outputs} outputs";

        return IsSilent
            ? $"Level check: only silence on {where} since the last check ({outputs} ready). If audio was playing, Windows is not sending it to this device."
            : $"Level check: heard sound on {where} (peak {Peak.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}, {outputs} playing).";
    }
}
