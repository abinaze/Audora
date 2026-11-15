using Audora.Core.Cleanup;

namespace Audora.Tests;

public class FrameStreamProcessorTests
{
    private sealed class FakeDenoiser : IFrameDenoiser
    {
        private readonly float _gain;
        private readonly float _speech;

        public FakeDenoiser(int frameSize, float gain = 1f, float speech = 0f)
        {
            FrameSize = frameSize;
            _gain = gain;
            _speech = speech;
        }

        public int FrameSize { get; }

        public int Frames { get; private set; }

        public float Process(float[] input, float[] output)
        {
            for (var i = 0; i < FrameSize; i++)
            {
                output[i] = input[i] * _gain;
            }

            Frames++;
            return _speech;
        }

        public void Dispose()
        {
        }
    }

    private static float[] Ramp(int count) => Enumerable.Range(1, count).Select(i => (float)i).ToArray();

    [Fact]
    public void TheOutputIsTheInputDelayedByOneFrame()
    {
        var processor = new FrameStreamProcessor(new FakeDenoiser(4));
        var samples = Ramp(12);

        processor.Process(samples);

        Assert.Equal(new float[] { 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 }, samples);
    }

    [Fact]
    public void TheChunkSizeDoesNotChangeTheOutput()
    {
        var whole = Ramp(40);
        new FrameStreamProcessor(new FakeDenoiser(4)).Process(whole);

        var pieces = Ramp(40);
        var processor = new FrameStreamProcessor(new FakeDenoiser(4));
        var start = 0;
        foreach (var size in new[] { 1, 3, 5, 7, 2, 11, 6, 5 })
        {
            processor.Process(pieces.AsSpan(start, size));
            start += size;
        }

        Assert.Equal(40, start);
        Assert.Equal(whole, pieces);
    }

    [Fact]
    public void TheDenoiserOnlySeesWholeFrames()
    {
        var denoiser = new FakeDenoiser(4);
        var processor = new FrameStreamProcessor(denoiser);

        processor.Process(Ramp(10));
        Assert.Equal(2, denoiser.Frames);

        processor.Process(Ramp(2));
        Assert.Equal(3, denoiser.Frames);
    }

    [Fact]
    public void WhatTheDenoiserReturnsIsWhatComesOut()
    {
        var processor = new FrameStreamProcessor(new FakeDenoiser(4, gain: 0.5f));
        var samples = Ramp(12);

        processor.Process(samples);

        Assert.Equal(new float[] { 0, 0, 0, 0, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f }, samples);
    }

    [Fact]
    public void ItRemembersTheLastSpeechProbability()
    {
        var processor = new FrameStreamProcessor(new FakeDenoiser(4, speech: 0.7f));
        Assert.Equal(0f, processor.LastSpeechProbability);

        processor.Process(Ramp(4));

        Assert.Equal(0.7f, processor.LastSpeechProbability);
    }

    [Fact]
    public void TheLatencyIsOneFrame()
    {
        Assert.Equal(480, new FrameStreamProcessor(new FakeDenoiser(480)).LatencySamples);
    }

    [Fact]
    public void AFrameSizeOfZeroIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new FrameStreamProcessor(new FakeDenoiser(0)));
    }
}
