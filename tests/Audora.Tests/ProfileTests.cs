using Audora.Core.Audio;
using NAudio.Wave;

namespace Audora.Tests;

public class ProfileTests
{
    private const int Rate = 48000;

    private sealed class Sine : ISampleProvider
    {
        private readonly double _frequency;
        private readonly double _amplitude;
        private long _n;

        public Sine(double frequency, double amplitude)
        {
            _frequency = frequency;
            _amplitude = amplitude;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i + 1 < count; i += 2)
            {
                var v = (float)(_amplitude * Math.Sin(2 * Math.PI * _frequency * _n++ / Rate));
                buffer[offset + i] = v;
                buffer[offset + i + 1] = v;
            }

            return count;
        }
    }

    private static double Rms(ISampleProvider provider)
    {
        var buffer = new float[Rate * 2];
        provider.Read(buffer, 0, buffer.Length);   // let everything settle
        provider.Read(buffer, 0, buffer.Length);
        provider.Read(buffer, 0, buffer.Length);

        double sum = 0;
        var frames = buffer.Length / 2;
        for (var f = 0; f < frames; f++)
        {
            sum += buffer[f * 2] * (double)buffer[f * 2];
        }

        return Math.Sqrt(sum / frames);
    }

    private static double Ratio(double frequency, double amplitude, EqSettings settings)
    {
        var eq = new EqualizerSampleProvider(new Sine(frequency, amplitude));
        eq.Apply(settings);
        return Rms(eq) / Rms(new Sine(frequency, amplitude));
    }

    private static EqSettings With(float voice = 0f, float stabilizer = 0f) =>
        new(true, 0f, new float[10], 0f, 0f, 1f, 0f, 0f, ReverbKind.Room, voice, stabilizer);

    [Fact]
    public void ThereAreSixProfiles_InTheOrderTheWindowShowsThem()
    {
        Assert.Equal(
            new[] { "Original", "Music", "Movie", "Communication", "Gaming", "Smart" },
            SoundPresets.All.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void EveryProfileHasTenBands() =>
        Assert.All(SoundPresets.All, p => Assert.Equal(10, p.Bands.Length));

    [Fact]
    public void OriginalAndSmartDoNotProcessAnythingThemselves()
    {
        Assert.False(SoundPresets.ToSettings(SoundPresets.All[0]).Enabled);
        Assert.False(SoundPresets.ToSettings(SoundPresets.All.Last()).Enabled);
    }

    [Fact]
    public void CommunicationIsCentredAndMovieIsWide()
    {
        var communication = SoundPresets.ToSettings(SoundPresets.All.First(p => p.Name == "Communication"));
        var movie = SoundPresets.ToSettings(SoundPresets.All.First(p => p.Name == "Movie"));

        Assert.Equal(0f, communication.StereoWidth);
        Assert.True(movie.StereoWidth > 1f);
        Assert.True(movie.Virtualizer > 0f);
    }

    [Fact]
    public void BoostingProfilesLowerThePreampToStayClean() =>
        Assert.True(SoundPresets.ToSettings(SoundPresets.All.First(p => p.Name == "Gaming")).PreampDb < 0f);

    [Fact]
    public void VoiceClarity_LiftsSpeechFrequenciesAndTrimsRumble()
    {
        Assert.InRange(Ratio(3000, 0.1, With(voice: 1f)), 1.3, 2.4);
        Assert.True(Ratio(100, 0.1, With(voice: 1f)) < 0.95);
    }

    [Fact]
    public void Stabilizer_RaisesQuietSoundsAndLowersLoudOnes()
    {
        Assert.InRange(Ratio(1000, 0.01, With(stabilizer: 1f)), 2.2, 3.8);   // about +9 dB
        Assert.InRange(Ratio(1000, 0.7, With(stabilizer: 1f)), 0.25, 0.65);  // about -7 dB
    }

    [Fact]
    public void Stabilizer_Off_ChangesNothing() =>
        Assert.InRange(Ratio(1000, 0.1, With()), 0.99, 1.01);

    [Fact]
    public void Stabilizer_StaysFinite()
    {
        var eq = new EqualizerSampleProvider(new Sine(200, 0.9));
        eq.Apply(With(stabilizer: 1f));
        var buffer = new float[Rate * 4];
        eq.Read(buffer, 0, buffer.Length);
        Assert.All(buffer, sample => Assert.True(float.IsFinite(sample) && Math.Abs(sample) < 4f));
    }

    [Theory]
    [InlineData("vlc", "", false, "Movie")]
    [InlineData("chrome", "Netflix - Google Chrome", false, "Movie")]
    [InlineData("chrome", "Lofi beats - YouTube Music", false, "Music")]
    [InlineData("chrome", "Cat video - YouTube", false, "Movie")]
    [InlineData("Spotify", "", false, "Music")]
    [InlineData("Teams", "", false, "Communication")]
    [InlineData("msedge", "Meet - abc-defg-hij", false, "Communication")]
    [InlineData("anything", "", true, "Gaming")]
    [InlineData("notepad", "notes", false, "Music")]
    public void SmartRules_PickTheRightProfile(string process, string title, bool fullscreen, string expected) =>
        Assert.Equal(expected, SmartRules.Choose(process, title, fullscreen));
}
