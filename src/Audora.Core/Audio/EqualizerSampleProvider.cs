using NAudio.Wave;

namespace Audora.Core.Audio;

/// <summary>
/// The sound shaper: 10-band equalizer with preamp, bass and treble, voice clarity, stereo width,
/// a 3D virtualizer, reverb and a volume stabilizer. When the settings are "Original" the samples
/// pass through untouched.
/// </summary>
public sealed class EqualizerSampleProvider : ISampleProvider
{
    private const int BandCount = 10;
    private const int Stages = BandCount + 2;   // 10 bands + bass shelf + treble shelf
    private const float BandQ = 1.41f;
    private const float BassHz = 110f;
    private const float TrebleHz = 8000f;
    private const float EffectThreshold = 0.001f;

    // What "voice clarity" adds to each band at full strength (dB): clearer speech, less rumble and mud.
    private static readonly float[] VoiceDelta = { 0f, 0f, -2f, -2f, -1f, 1f, 3f, 4f, 1f, 0f };

    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly int _rate;
    private readonly float _sampleRate;
    private readonly float[] _z1;
    private readonly float[] _z2;
    private readonly BiquadCoefficients[] _stages = new BiquadCoefficients[Stages];
    private readonly VirtualizerProcessor? _virtualizer;
    private readonly ReverbProcessor? _reverb;
    private readonly StabilizerProcessor _stabilizer;
    private volatile EqSettings _requested = EqSettings.Original;
    private EqSettings? _built;
    private float _preamp = 1f;
    private float _width = 1f;
    private float _virtualizerAmount;
    private float _reverbAmount;
    private float _stabilizerAmount;
    private float _dry = 1f;
    private float _wet;
    private bool _effects;

    public EqualizerSampleProvider(ISampleProvider source)
    {
        _source = source;
        _channels = source.WaveFormat.Channels;
        _rate = source.WaveFormat.SampleRate;
        _sampleRate = _rate;
        _z1 = new float[Stages * _channels];
        _z2 = new float[Stages * _channels];
        Array.Fill(_stages, BiquadCoefficients.Identity);
        _stabilizer = new StabilizerProcessor(_rate);

        // the 3D and reverb effects are made for stereo
        if (_channels == 2)
        {
            _virtualizer = new VirtualizerProcessor(_rate);
            _reverb = new ReverbProcessor(_rate);
        }
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Takes effect on the next block of audio.</summary>
    public void Apply(EqSettings settings) => _requested = settings;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);

        var settings = _requested;
        if (!settings.Enabled)
        {
            if (_built is not null)
            {
                Array.Clear(_z1);
                Array.Clear(_z2);
                _virtualizer?.Reset();
                _reverb?.Reset();
                _stabilizer.Reset();
                _built = null;
            }

            return read;
        }

        if (!ReferenceEquals(settings, _built))
        {
            Build(settings);
        }

        var channels = _channels;
        var widen = channels == 2 && Math.Abs(_width - 1f) > 0.001f;

        for (var i = 0; i + channels <= read; i += channels)
        {
            if (widen)
            {
                var left = buffer[offset + i];
                var right = buffer[offset + i + 1];
                var mid = (left + right) * 0.5f;
                var side = (left - right) * 0.5f * _width;
                buffer[offset + i] = mid + side;
                buffer[offset + i + 1] = mid - side;
            }

            for (var ch = 0; ch < channels; ch++)
            {
                var x = buffer[offset + i + ch] * _preamp;

                for (var s = 0; s < Stages; s++)
                {
                    var c = _stages[s];
                    var k = s * channels + ch;
                    var y = c.B0 * x + _z1[k];
                    _z1[k] = c.B1 * x - c.A1 * y + _z2[k];
                    _z2[k] = c.B2 * x - c.A2 * y;

                    // keep the filter memory from sinking into very slow tiny numbers during silence
                    if (Math.Abs(_z1[k]) < 1e-15f) { _z1[k] = 0f; }
                    if (Math.Abs(_z2[k]) < 1e-15f) { _z2[k] = 0f; }

                    x = y;
                }

                buffer[offset + i + ch] = x;
            }

            if (_effects)
            {
                var l = buffer[offset + i];
                var r = buffer[offset + i + 1];

                if (_virtualizerAmount > EffectThreshold)
                {
                    _virtualizer!.Process(ref l, ref r, _virtualizerAmount);
                }

                if (_reverbAmount > EffectThreshold)
                {
                    _reverb!.Process(l, r, out var wetLeft, out var wetRight);
                    l = l * _dry + wetLeft * _wet;
                    r = r * _dry + wetRight * _wet;
                }

                buffer[offset + i] = l;
                buffer[offset + i + 1] = r;
            }

            if (_stabilizerAmount > EffectThreshold)
            {
                var peak = 0f;
                for (var ch = 0; ch < channels; ch++)
                {
                    peak = Math.Max(peak, Math.Abs(buffer[offset + i + ch]));
                }

                var gain = _stabilizer.GainFor(peak, _stabilizerAmount);
                for (var ch = 0; ch < channels; ch++)
                {
                    buffer[offset + i + ch] *= gain;
                }
            }
        }

        return read;
    }

    private void Build(EqSettings settings)
    {
        var clarity = Math.Clamp(settings.VoiceClarity, 0f, 1f);

        for (var i = 0; i < BandCount; i++)
        {
            var gain = (i < settings.BandsDb.Count ? settings.BandsDb[i] : 0f) + VoiceDelta[i] * clarity;
            _stages[i] = BiquadCoefficients.Peaking(_sampleRate, EqSettings.BandFrequencies[i], gain, BandQ);
        }

        _stages[BandCount] = BiquadCoefficients.LowShelf(_sampleRate, BassHz, settings.BassDb);
        _stages[BandCount + 1] = BiquadCoefficients.HighShelf(_sampleRate, TrebleHz, settings.TrebleDb);
        _preamp = MathF.Pow(10f, settings.PreampDb / 20f);
        _width = Math.Clamp(settings.StereoWidth, 0f, 2f);

        _virtualizerAmount = Math.Clamp(settings.Virtualizer, 0f, 1f);
        _reverbAmount = Math.Clamp(settings.ReverbAmount, 0f, 1f);
        _stabilizerAmount = Math.Clamp(settings.Stabilizer, 0f, 1f);
        _reverb?.Configure(settings.Room);

        if (_virtualizerAmount <= EffectThreshold)
        {
            _virtualizer?.Reset();
        }

        if (_reverbAmount <= EffectThreshold)
        {
            _reverb?.Reset();
        }

        if (_stabilizerAmount <= EffectThreshold)
        {
            _stabilizer.Reset();
        }

        _dry = 1f - 0.25f * _reverbAmount;
        _wet = 1.2f * _reverbAmount;
        _effects = _channels == 2 && (_virtualizerAmount > EffectThreshold || _reverbAmount > EffectThreshold);
        _built = settings;
    }
}
