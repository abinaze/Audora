using System.Globalization;

namespace Audora.Core.Audio;

/// <summary>Turns microphone loudness (RMS, 0 to 1) into a meter position and plain words.</summary>
public static class MicrophoneMeter
{
    /// <summary>The meter is empty at this level and full at 0 dB.</summary>
    public const double FloorDb = -60.0;

    /// <summary>Loudness in decibels. Silence and bad numbers read -120.</summary>
    public static double Db(float rms)
    {
        if (!float.IsFinite(rms))
        {
            return -120.0;
        }

        return 20.0 * Math.Log10(Math.Max(rms, 1e-6f));
    }

    /// <summary>A meter position from 0 (empty) to 1 (full).</summary>
    public static double Bar(float rms)
    {
        if (!float.IsFinite(rms) || rms <= 0f)
        {
            return 0.0;
        }

        return Math.Clamp((Db(rms) - FloorDb) / -FloorDb, 0.0, 1.0);
    }

    /// <summary>Says how much quieter the cleaned sound is than the sound that came in.</summary>
    public static string ChangeText(float inputRms, float outputRms)
    {
        if (Db(inputRms) < FloorDb)
        {
            return "Too quiet to compare.";
        }

        var change = Db(outputRms) - Db(inputRms);
        if (change > -1.0)
        {
            return "Nothing was removed.";
        }

        return "Quieter by " + Math.Abs(change).ToString("0.0", CultureInfo.InvariantCulture) + " dB.";
    }
}
