namespace Audora.Core.Audio;

/// <summary>
/// Room reverb (the well-known Freeverb design: eight comb filters and four all-pass filters per ear).
/// Gives only the reverb part of the sound; the caller mixes it with the original.
/// </summary>
public sealed class ReverbProcessor
{
    private static readonly int[] CombTunings = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    private static readonly int[] AllpassTunings = { 556, 441, 341, 225 };
    private const int StereoSpread = 23;
    private const float InputGain = 0.015f;

    private readonly Comb[] _combLeft = new Comb[CombTunings.Length];
    private readonly Comb[] _combRight = new Comb[CombTunings.Length];
    private readonly Allpass[] _allpassLeft = new Allpass[AllpassTunings.Length];
    private readonly Allpass[] _allpassRight = new Allpass[AllpassTunings.Length];
    private float _feedback = 0.8f;
    private float _damping = 0.5f;

    public ReverbProcessor(int sampleRate)
    {
        var scale = sampleRate / 44100.0;
        for (var i = 0; i < CombTunings.Length; i++)
        {
            _combLeft[i] = new Comb(Scaled(CombTunings[i], scale));
            _combRight[i] = new Comb(Scaled(CombTunings[i] + StereoSpread, scale));
        }

        for (var i = 0; i < AllpassTunings.Length; i++)
        {
            _allpassLeft[i] = new Allpass(Scaled(AllpassTunings[i], scale));
            _allpassRight[i] = new Allpass(Scaled(AllpassTunings[i] + StereoSpread, scale));
        }

        Configure(ReverbKind.Room);
    }

    /// <summary>Picks how long and how bright the echo is.</summary>
    public void Configure(ReverbKind kind)
    {
        var (size, damping) = kind switch
        {
            ReverbKind.Hall => (0.80f, 0.30f),
            ReverbKind.Stage => (0.60f, 0.45f),
            _ => (0.35f, 0.55f)
        };

        _feedback = 0.7f + 0.28f * size;   // always below 1, so the echo always fades away
        _damping = damping;
    }

    public void Reset()
    {
        foreach (var comb in _combLeft) { comb.Clear(); }
        foreach (var comb in _combRight) { comb.Clear(); }
        foreach (var allpass in _allpassLeft) { allpass.Clear(); }
        foreach (var allpass in _allpassRight) { allpass.Clear(); }
    }

    public void Process(float left, float right, out float wetLeft, out float wetRight)
    {
        var input = (left + right) * InputGain;
        float l = 0f;
        float r = 0f;

        for (var i = 0; i < _combLeft.Length; i++)
        {
            l += _combLeft[i].Process(input, _feedback, _damping);
            r += _combRight[i].Process(input, _feedback, _damping);
        }

        for (var i = 0; i < _allpassLeft.Length; i++)
        {
            l = _allpassLeft[i].Process(l);
            r = _allpassRight[i].Process(r);
        }

        wetLeft = l;
        wetRight = r;
    }

    private static int Scaled(int samples, double scale) => Math.Max(1, (int)Math.Round(samples * scale));

    private sealed class Comb
    {
        private readonly float[] _buffer;
        private int _index;
        private float _store;

        public Comb(int size) => _buffer = new float[size];

        public float Process(float input, float feedback, float damping)
        {
            var output = _buffer[_index];
            _store = output * (1f - damping) + _store * damping;
            if (Math.Abs(_store) < 1e-20f)
            {
                _store = 0f;
            }

            _buffer[_index] = input + _store * feedback;
            if (++_index >= _buffer.Length)
            {
                _index = 0;
            }

            return output;
        }

        public void Clear()
        {
            Array.Clear(_buffer);
            _store = 0f;
            _index = 0;
        }
    }

    private sealed class Allpass
    {
        private readonly float[] _buffer;
        private int _index;

        public Allpass(int size) => _buffer = new float[size];

        public float Process(float input)
        {
            var buffered = _buffer[_index];
            var output = -input + buffered;
            _buffer[_index] = input + buffered * 0.5f;
            if (++_index >= _buffer.Length)
            {
                _index = 0;
            }

            return output;
        }

        public void Clear()
        {
            Array.Clear(_buffer);
            _index = 0;
        }
    }
}
