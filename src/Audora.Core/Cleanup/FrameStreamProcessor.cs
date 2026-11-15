namespace Audora.Core.Cleanup;

/// <summary>
/// Feeds a stream of samples of any chunk size to a denoiser that only takes whole frames.
/// The output is the input cleaned and delayed by exactly one frame, whatever the chunk sizes are, so the
/// delay never changes while it runs. The first frame of output is silence.
/// </summary>
public sealed class FrameStreamProcessor
{
    private readonly IFrameDenoiser _denoiser;
    private readonly int _frameSize;
    private readonly float[] _frameIn;
    private readonly float[] _frameOut;
    private readonly float[] _ready;
    private int _filled;
    private int _readPosition;
    private int _readyCount;

    public FrameStreamProcessor(IFrameDenoiser denoiser)
    {
        ArgumentNullException.ThrowIfNull(denoiser);
        if (denoiser.FrameSize <= 0)
        {
            throw new ArgumentException("The frame size must be above zero.", nameof(denoiser));
        }

        _denoiser = denoiser;
        _frameSize = denoiser.FrameSize;
        _frameIn = new float[_frameSize];
        _frameOut = new float[_frameSize];
        _ready = new float[_frameSize * 2];
        _readyCount = _frameSize; // one frame of silence, so there is always a sample to hand out
    }

    /// <summary>How many samples later than the input the cleaned sound comes out.</summary>
    public int LatencySamples => _frameSize;

    /// <summary>The speech probability of the last frame that was cleaned (0 before the first one).</summary>
    public float LastSpeechProbability { get; private set; }

    /// <summary>Cleans the samples in place. They must be mono and in the sample rate the denoiser expects.</summary>
    public void Process(Span<float> samples)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            var incoming = samples[i];
            samples[i] = _ready[_readPosition];
            _readPosition = (_readPosition + 1) % _ready.Length;
            _readyCount--;

            _frameIn[_filled++] = incoming;
            if (_filled < _frameSize)
            {
                continue;
            }

            LastSpeechProbability = _denoiser.Process(_frameIn, _frameOut);
            var write = (_readPosition + _readyCount) % _ready.Length;
            for (var j = 0; j < _frameSize; j++)
            {
                _ready[write] = _frameOut[j];
                write = (write + 1) % _ready.Length;
            }

            _readyCount += _frameSize;
            _filled = 0;
        }
    }
}
