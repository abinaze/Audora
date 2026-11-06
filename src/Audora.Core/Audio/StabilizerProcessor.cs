namespace Audora.Core.Audio;

/// <summary>
/// Volume stabilizer: a compressor that brings loud sounds down and quiet sounds up, so speech and
/// explosions end up closer in loudness. It watches the loudest channel, so the stereo picture stays put.
/// </summary>
public sealed class StabilizerProcessor
{
    private readonly float _attack;
    private readonly float _release;
    private float _envelope;

    public StabilizerProcessor(int sampleRate)
    {
        _attack = MathF.Exp(-1f / (0.008f * sampleRate));    // reacts to loud sounds in about 8 ms
        _release = MathF.Exp(-1f / (0.200f * sampleRate));   // lets go over about 200 ms
    }

    public void Reset() => _envelope = 0f;

    /// <summary>The gain to apply to this frame. peak is its loudest channel, amount runs from 0 to 1.</summary>
    public float GainFor(float peak, float amount)
    {
        var level = Math.Abs(peak);
        var coefficient = level > _envelope ? _attack : _release;
        _envelope = level + coefficient * (_envelope - level);

        var thresholdDb = -12f - 14f * amount;
        var ratio = 1f + 3f * amount;
        var envelopeDb = 20f * MathF.Log10(Math.Max(_envelope, 1e-6f));
        var reductionDb = envelopeDb > thresholdDb ? (envelopeDb - thresholdDb) * (1f - 1f / ratio) : 0f;
        var makeupDb = -thresholdDb * (1f - 1f / ratio) * 0.5f;   // brings quiet parts up

        return MathF.Pow(10f, (makeupDb - reductionDb) / 20f);
    }
}
