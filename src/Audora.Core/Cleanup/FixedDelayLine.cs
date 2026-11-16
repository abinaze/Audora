namespace Audora.Core.Cleanup;

/// <summary>Hands every sample back a fixed number of samples later. The first samples come out as silence.</summary>
internal sealed class FixedDelayLine
{
    private readonly float[] _ring;
    private int _position;

    public FixedDelayLine(int delay)
    {
        if (delay < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "The delay must be at least one sample.");
        }

        _ring = new float[delay];
    }

    public float Push(float sample)
    {
        var old = _ring[_position];
        _ring[_position] = sample;
        _position = (_position + 1) % _ring.Length;
        return old;
    }
}
