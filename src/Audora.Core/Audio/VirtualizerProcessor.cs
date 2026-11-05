namespace Audora.Core.Audio;

/// <summary>
/// A simple "3D" effect for stereo headphones: it widens the stereo picture and adds soft, slightly
/// late reflections from the opposite side, so the sound feels less inside the head.
/// This is a widening effect, not true surround sound.
/// </summary>
public sealed class VirtualizerProcessor
{
    private readonly float[] _delayLeft;
    private readonly float[] _delayRight;
    private readonly float _softening;
    private int _position;
    private float _softLeft;
    private float _softRight;

    public VirtualizerProcessor(int sampleRate)
    {
        var length = Math.Max(2, (int)(sampleRate * 0.010));   // reflections arrive 10 ms late
        _delayLeft = new float[length];
        _delayRight = new float[length];
        _softening = 1f - MathF.Exp(-2f * MathF.PI * 3000f / sampleRate);   // rounds off the highs
    }

    public void Reset()
    {
        Array.Clear(_delayLeft);
        Array.Clear(_delayRight);
        _softLeft = 0f;
        _softRight = 0f;
        _position = 0;
    }

    /// <summary>amount runs from 0 (off) to 1 (strongest).</summary>
    public void Process(ref float left, ref float right, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);

        // wider: boost what differs between the two ears
        var mid = (left + right) * 0.5f;
        var side = (left - right) * 0.5f * (1f + 0.8f * amount);
        var l = mid + side;
        var r = mid - side;

        // reflections: each ear also hears the other side, 10 ms later and softer
        var crossLeft = _delayRight[_position];
        var crossRight = _delayLeft[_position];
        _softLeft += _softening * (crossLeft - _softLeft);
        _softRight += _softening * (crossRight - _softRight);
        _delayLeft[_position] = l;
        _delayRight[_position] = r;
        _position = (_position + 1) % _delayLeft.Length;

        var mix = 0.35f * amount;
        var level = 1f / (1f + mix);   // keeps the loudness about the same
        left = (l + _softLeft * mix) * level;
        right = (r + _softRight * mix) * level;
    }
}
