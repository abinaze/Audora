namespace Audora.Core.Cleanup;

/// <summary>Removes noise from one channel of speech, one frame at a time.</summary>
public interface IFrameDenoiser : IDisposable
{
    /// <summary>Samples in every frame. RNNoise uses 480, which is 10 ms at 48 kHz.</summary>
    int FrameSize { get; }

    /// <summary>
    /// Extra samples the denoiser itself holds back, on top of the one frame that the stream wrapper adds. Zero unless
    /// the model looks ahead. The original sound is delayed by the same amount so the two line up when they are mixed.
    /// </summary>
    int AlgorithmicDelaySamples => 0;

    /// <summary>
    /// Cleans one frame. Both arrays hold exactly <see cref="FrameSize"/> samples between -1 and 1.
    /// Returns how likely the frame holds speech, from 0 to 1.
    /// </summary>
    float Process(float[] input, float[] output);
}
